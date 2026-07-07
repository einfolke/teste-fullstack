using StackExchange.Redis;

namespace Parking.Api.Services
{
    // Lock distribuído: garante que apenas uma instância execute uma seção crítica
    // (ex.: geração de faturas de uma competência) mesmo com a API escalada horizontalmente.
    public interface IDistributedLock
    {
        // Retorna um handle a ser liberado (dispose) ao final; null se o lock já estiver tomado.
        Task<IAsyncDisposable?> AdquirirAsync(string chave, TimeSpan expiracao, CancellationToken ct = default);
    }

    // Implementação baseada em Redis usando o padrão SET key token NX PX.
    // A liberação é feita via script Lua comparando o token (só libera se ainda for o dono).
    public class RedisDistributedLock : IDistributedLock
    {
        private const string ScriptLiberacao = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        private readonly IConnectionMultiplexer _redis;
        public RedisDistributedLock(IConnectionMultiplexer redis) => _redis = redis;

        public async Task<IAsyncDisposable?> AdquirirAsync(string chave, TimeSpan expiracao, CancellationToken ct = default)
        {
            var db = _redis.GetDatabase();
            var token = Guid.NewGuid().ToString("N");

            var adquirido = await db.StringSetAsync(chave, token, expiracao, When.NotExists);
            if (!adquirido) return null;

            return new Handle(db, chave, token);
        }

        private sealed class Handle : IAsyncDisposable
        {
            private readonly IDatabase _db;
            private readonly string _chave;
            private readonly string _token;
            public Handle(IDatabase db, string chave, string token) { _db = db; _chave = chave; _token = token; }

            public async ValueTask DisposeAsync()
            {
                await _db.ScriptEvaluateAsync(
                    ScriptLiberacao,
                    new RedisKey[] { _chave },
                    new RedisValue[] { _token });
            }
        }
    }

    // Fallback usado quando não há Redis configurado: sempre "adquire" (nenhuma coordenação distribuída).
    public class NoOpDistributedLock : IDistributedLock
    {
        private sealed class Vazio : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        public Task<IAsyncDisposable?> AdquirirAsync(string chave, TimeSpan expiracao, CancellationToken ct = default)
            => Task.FromResult<IAsyncDisposable?>(new Vazio());
    }
}
