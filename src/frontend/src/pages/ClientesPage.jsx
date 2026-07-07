import React, { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPost, apiPut, apiDelete } from '../api'

const emptyForm = { nome:'', telefone:'', endereco:'', mensalista:false, valorMensalidade:'', ativo:true }

export default function ClientesPage(){
  const qc = useQueryClient()
  const [filtro, setFiltro] = useState('')
  const [mensalista, setMensalista] = useState('all')
  const [form, setForm] = useState(emptyForm)
  const [modalAberto, setModalAberto] = useState(false)
  const [editId, setEditId] = useState(null)
  const [editForm, setEditForm] = useState(emptyForm)
  const [erro, setErro] = useState('')

  const q = useQuery({
    queryKey:['clientes', filtro, mensalista],
    queryFn:() => apiGet(`/api/clientes?pagina=1&tamanho=20&filtro=${encodeURIComponent(filtro)}&mensalista=${mensalista}`)
  })

  const create = useMutation({
    mutationFn: (data) => apiPost('/api/clientes', data),
    onSuccess: () => {
      setErro('')
      setForm(emptyForm)
      setModalAberto(false)
      qc.invalidateQueries({ queryKey:['clientes'] })
    },
    onError: (e) => setErro(e.message || 'Erro ao salvar cliente.')
  })

  const atualizar = useMutation({
    mutationFn: ({ id, data }) => apiPut(`/api/clientes/${id}`, data),
    onSuccess: () => {
      setErro('')
      setEditId(null)
      qc.invalidateQueries({ queryKey:['clientes'] })
    },
    onError: (e) => setErro(e.message || 'Erro ao atualizar cliente.')
  })

  const remover = useMutation({
    mutationFn: (id) => apiDelete(`/api/clientes/${id}`),
    onSuccess: () => {
      setErro('')
      qc.invalidateQueries({ queryKey:['clientes'] })
    },
    onError: (e) => setErro(e.message || 'Erro ao excluir cliente.')
  })

  function validar(f){
    if(!f.nome.trim()) return 'O nome é obrigatório.'
    if(f.mensalista && (!f.valorMensalidade || Number(f.valorMensalidade) <= 0))
      return 'Informe um valor de mensalidade maior que zero para clientes mensalistas.'
    return ''
  }

  function toPayload(f){
    return {
      nome: f.nome.trim(),
      telefone: f.telefone.trim() || null,
      endereco: f.endereco.trim() || null,
      mensalista: f.mensalista,
      valorMensalidade: f.mensalista && f.valorMensalidade ? Number(f.valorMensalidade) : null,
      ativo: f.ativo
    }
  }

  function salvarNovo(){
    const msg = validar(form)
    if(msg){ setErro(msg); return }
    create.mutate(toPayload(form))
  }

  function abrirModal(){
    setErro('')
    setForm(emptyForm)
    setModalAberto(true)
  }

  function fecharModal(){
    setModalAberto(false)
    setErro('')
  }

  function iniciarEdicao(c){
    setErro('')
    setEditId(c.id)
    setEditForm({
      nome: c.nome ?? '',
      telefone: c.telefone ?? '',
      endereco: c.endereco ?? '',
      mensalista: c.mensalista,
      valorMensalidade: c.valorMensalidade ?? '',
      ativo: c.ativo ?? true
    })
  }

  function salvarEdicao(){
    const msg = validar(editForm)
    if(msg){ setErro(msg); return }
    atualizar.mutate({ id: editId, data: toPayload(editForm) })
  }

  function alternarStatus(c){
    setErro('')
    atualizar.mutate({ id: c.id, data: toPayload({
      nome: c.nome ?? '',
      telefone: c.telefone ?? '',
      endereco: c.endereco ?? '',
      mensalista: c.mensalista,
      valorMensalidade: c.valorMensalidade ?? '',
      ativo: !(c.ativo ?? true)
    }) })
  }

  return (
    <div>
      <h2>Clientes</h2>

      {erro && !modalAberto && <div className="section" style={{background:'#fde8e8', color:'#b91c1c', border:'1px solid #f5c2c2'}}>{erro}</div>}

      <div className="section">
        <div className="grid grid-3">
          <input placeholder="Buscar por nome" value={filtro} onChange={e=>setFiltro(e.target.value)} />
          <select value={mensalista} onChange={e=>setMensalista(e.target.value)}>
            <option value="all">Todos</option>
            <option value="true">Mensalistas</option>
            <option value="false">Não mensalistas</option>
          </select>
          <div style={{display:'flex', justifyContent:'flex-end', alignItems:'center'}}>
            <button onClick={abrirModal}>+ Novo cliente</button>
          </div>
        </div>
      </div>

      <h3 style={{marginTop:16}}>Lista</h3>
      <div className="section">
        {q.isLoading? <p>Carregando...</p> : (
          <table>
            <thead><tr><th>Nome</th><th>Telefone</th><th>Endereço</th><th>Mensalista</th><th>Mensalidade</th><th>Status</th><th></th></tr></thead>
            <tbody>
              {q.data.itens.map(c=> editId === c.id ? (
                <tr key={c.id}>
                  <td><input value={editForm.nome} onChange={e=>setEditForm({...editForm, nome:e.target.value})}/></td>
                  <td><input value={editForm.telefone} onChange={e=>setEditForm({...editForm, telefone:e.target.value})}/></td>
                  <td><input value={editForm.endereco} onChange={e=>setEditForm({...editForm, endereco:e.target.value})}/></td>
                  <td>
                    <label style={{display:'flex', alignItems:'center', gap:6}}>
                      <input type="checkbox" checked={editForm.mensalista} onChange={e=>setEditForm({...editForm, mensalista:e.target.checked})}/> Sim
                    </label>
                  </td>
                  <td><input value={editForm.valorMensalidade} onChange={e=>setEditForm({...editForm, valorMensalidade:e.target.value})}/></td>
                  <td>
                    <label style={{display:'flex', alignItems:'center', gap:6}}>
                      <input type="checkbox" checked={editForm.ativo} onChange={e=>setEditForm({...editForm, ativo:e.target.checked})}/> Ativo
                    </label>
                  </td>
                  <td style={{display:'flex', gap:8}}>
                    <button onClick={salvarEdicao} disabled={atualizar.isPending}>Salvar</button>
                    <button className="btn-ghost" onClick={()=>{ setEditId(null); setErro('') }}>Cancelar</button>
                  </td>
                </tr>
              ) : (
                <tr key={c.id}>
                  <td>{c.nome}</td>
                  <td>{c.telefone}</td>
                  <td>{c.endereco}</td>
                  <td>{c.mensalista? 'Sim':'Não'}</td>
                  <td>{c.valorMensalidade != null ? Number(c.valorMensalidade).toFixed(2) : '-'}</td>
                  <td>
                    <span className={`badge ${ (c.ativo ?? true) ? 'badge-ativo' : 'badge-inativo' }`}>
                      {(c.ativo ?? true) ? 'Ativo' : 'Inativo'}
                    </span>
                  </td>
                  <td style={{display:'flex', gap:8}}>
                    <button onClick={()=>iniciarEdicao(c)}>Editar</button>
                    <button className="btn-ghost" onClick={()=>alternarStatus(c)}>{(c.ativo ?? true) ? 'Inativar' : 'Ativar'}</button>
                    <button className="btn-ghost" onClick={()=>remover.mutate(c.id)}>Excluir</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {modalAberto && (
        <div className="modal-overlay" onClick={fecharModal}>
          <div className="modal" onClick={e=>e.stopPropagation()}>
            <div className="modal-header">
              <h3>Novo cliente</h3>
              <button className="modal-close" onClick={fecharModal} aria-label="Fechar">×</button>
            </div>

            {erro && <div className="section" style={{background:'#fde8e8', color:'#b91c1c', border:'1px solid #f5c2c2'}}>{erro}</div>}

            <div className="modal-body">
              <div className="field">
                <label>Nome</label>
                <input placeholder="Nome" value={form.nome} onChange={e=>setForm({...form, nome:e.target.value})}/>
              </div>
              <div className="field">
                <label>Telefone</label>
                <input placeholder="Telefone" value={form.telefone} onChange={e=>setForm({...form, telefone:e.target.value})}/>
              </div>
              <div className="field">
                <label>Endereço</label>
                <input placeholder="Endereço" value={form.endereco} onChange={e=>setForm({...form, endereco:e.target.value})}/>
              </div>
              <label style={{display:'flex', alignItems:'center', gap:8}}>
                <input type="checkbox" checked={form.mensalista} onChange={e=>setForm({...form, mensalista:e.target.checked})}/> Mensalista
              </label>
              <label style={{display:'flex', alignItems:'center', gap:8}}>
                <input type="checkbox" checked={form.ativo} onChange={e=>setForm({...form, ativo:e.target.checked})}/> Ativo
              </label>
              {form.mensalista && (
                <div className="field">
                  <label>Valor mensalidade</label>
                  <input placeholder="Valor mensalidade" value={form.valorMensalidade} onChange={e=>setForm({...form, valorMensalidade:e.target.value})}/>
                </div>
              )}
            </div>

            <div className="modal-footer">
              <button className="btn-ghost" onClick={fecharModal}>Cancelar</button>
              <button onClick={salvarNovo} disabled={create.isPending}>Salvar</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
