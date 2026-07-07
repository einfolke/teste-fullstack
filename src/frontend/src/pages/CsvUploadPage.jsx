import React, { useState } from 'react'

export default function CsvUploadPage(){
  const [log, setLog] = useState(null)
  const [erro, setErro] = useState('')
  const [enviando, setEnviando] = useState(false)

  async function handleUpload(e){
    e.preventDefault()
    setErro('')
    setLog(null)
    const file = e.target.file.files[0]
    if(!file){ setErro('Selecione um arquivo CSV antes de enviar.'); return }

    const fd = new FormData()
    fd.append('file', file)
    setEnviando(true)
    try {
      const r = await fetch((import.meta.env.VITE_API_URL || 'http://localhost:5000') + '/api/import/csv', {
        method: 'POST',
        body: fd
      })
      if(!r.ok){
        const texto = await r.text()
        setErro(texto || 'Falha ao importar o arquivo.')
        return
      }
      const j = await r.json()
      setLog(j)
    } catch (err) {
      setErro(err.message || 'Erro de comunicação com o servidor.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div>
      <h2>Importar CSV</h2>
      <div className="section">
        <form onSubmit={handleUpload} style={{display:'flex', gap:10, alignItems:'center'}}>
          <input type="file" name="file" accept=".csv" />
          <button type="submit" disabled={enviando}>{enviando ? 'Enviando...' : 'Enviar'}</button>
        </form>
      </div>

      {erro && <div className="section" style={{background:'#fde8e8', color:'#b91c1c', border:'1px solid #f5c2c2'}}>{erro}</div>}

      <h3 style={{marginTop:16}}>Relatório</h3>
      <div className="section">
        {!log ? (
          <p className="note">Aguardando upload...</p>
        ) : (
          <>
            <div style={{display:'flex', gap:16, flexWrap:'wrap', marginBottom:12}}>
              <span><strong>Processados:</strong> {log.processados}</span>
              <span style={{color:'#15803d'}}><strong>Inseridos:</strong> {log.inseridos}</span>
              <span style={{color: log.falhas ? '#b91c1c' : 'inherit'}}><strong>Falhas:</strong> {log.falhas}</span>
            </div>

            {log.falhas > 0 ? (
              <table>
                <thead>
                  <tr><th style={{width:70}}>Linha</th><th style={{width:160}}>Coluna</th><th>Motivo</th><th>Conteúdo</th></tr>
                </thead>
                <tbody>
                  {log.erros.map((e, i) => (
                    <tr key={i}>
                      <td>{e.linha}</td>
                      <td>{e.coluna || '-'}</td>
                      <td style={{color:'#b91c1c'}}>{e.motivo}</td>
                      <td style={{fontFamily:'monospace', fontSize:12, color:'#475569'}}>{e.conteudo}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p style={{color:'#15803d', margin:0}}>Importação concluída sem erros.</p>
            )}
          </>
        )}
      </div>
    </div>
  )
}
