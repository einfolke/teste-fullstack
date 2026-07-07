import React, { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPost, apiPut, apiDelete } from '../api'

function ClienteCombobox({ clientes, value, onChange, placeholder = 'Buscar cliente...' }){
  const [aberto, setAberto] = useState(false)
  const [busca, setBusca] = useState('')
  const [rect, setRect] = useState(null)
  const ref = useRef(null)
  const inputRef = useRef(null)

  const selecionado = clientes.find(c => c.id === value)
  const texto = aberto ? busca : (selecionado?.nome ?? '')

  const normalizar = (s) => (s ?? '')
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')

  const termo = normalizar(busca.trim())
  const filtrados = termo
    ? clientes.filter(c => normalizar(c.nome).includes(termo))
    : clientes

  function atualizarPosicao(){
    if(inputRef.current) setRect(inputRef.current.getBoundingClientRect())
  }

  function abrir(){
    setAberto(true)
    setBusca('')
    atualizarPosicao()
  }

  useEffect(()=>{
    function onClickFora(e){
      if(ref.current && !ref.current.contains(e.target)){ setAberto(false); setBusca('') }
    }
    document.addEventListener('mousedown', onClickFora)
    return () => document.removeEventListener('mousedown', onClickFora)
  }, [])

  useEffect(()=>{
    if(!aberto) return
    const handler = () => atualizarPosicao()
    window.addEventListener('scroll', handler, true)
    window.addEventListener('resize', handler)
    return () => {
      window.removeEventListener('scroll', handler, true)
      window.removeEventListener('resize', handler)
    }
  }, [aberto])

  return (
    <div className="combobox" ref={ref}>
      <input
        ref={inputRef}
        placeholder={placeholder}
        value={texto}
        onFocus={abrir}
        onChange={e=>{ setBusca(e.target.value); setAberto(true); atualizarPosicao() }}
      />
      {aberto && rect && (
        <ul
          className="combobox-list"
          style={{ position:'fixed', top: rect.bottom + 4, left: rect.left, width: rect.width }}
        >
          {filtrados.length === 0 && <li className="combobox-empty">Nenhum cliente encontrado</li>}
          {filtrados.map(c => (
            <li
              key={c.id}
              className={c.id === value ? 'is-selected' : ''}
              onMouseDown={()=>{ onChange(c.id); setAberto(false); setBusca('') }}
            >
              {c.nome}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

export default function VeiculosPage(){
  const qc = useQueryClient()
  const [clienteId, setClienteId] = useState('')
  const clientes = useQuery({ queryKey:['clientes-mini'], queryFn:() => apiGet('/api/clientes?pagina=1&tamanho=100') })
  const veiculos = useQuery({ queryKey:['veiculos', clienteId], queryFn:() => apiGet(`/api/veiculos${clienteId?`?clienteId=${clienteId}`:''}`) })
  const [form, setForm] = useState({ placa:'', modelo:'', ano:'', clienteId:'', ativo:true })
  const [editId, setEditId] = useState(null)
  const [editForm, setEditForm] = useState({ placa:'', modelo:'', ano:'', clienteId:'', ativo:true })
  const [erro, setErro] = useState('')

  const create = useMutation({
    mutationFn: (data) => apiPost('/api/veiculos', data),
    onSuccess: () => { setErro(''); qc.invalidateQueries({ queryKey:['veiculos'] }) },
    onError: (e) => setErro(e.message || 'Erro ao salvar veículo.')
  })
  const update = useMutation({
    mutationFn: ({id, data}) => apiPut(`/api/veiculos/${id}`, data),
    onSuccess: () => { setErro(''); setEditId(null); qc.invalidateQueries({ queryKey:['veiculos'] }) },
    onError: (e) => setErro(e.message || 'Erro ao atualizar veículo.')
  })
  const remover = useMutation({
    mutationFn: (id) => apiDelete(`/api/veiculos/${id}`),
    onSuccess: () => { setErro(''); qc.invalidateQueries({ queryKey:['veiculos'] }) },
    onError: (e) => setErro(e.message || 'Erro ao excluir veículo.')
  })

  useEffect(()=>{
    if(clientes.data?.itens?.length && !clienteId){
      setClienteId(clientes.data.itens[0].id)
      setForm(f => ({...f, clienteId: clientes.data.itens[0].id}))
    }
  }, [clientes.data])

  const listaClientes = clientes.data?.itens ?? []
  const nomeCliente = (id) => listaClientes.find(c => c.id === id)?.nome ?? id

  function iniciarEdicao(v){
    setErro('')
    setEditId(v.id)
    setEditForm({ placa: v.placa ?? '', modelo: v.modelo ?? '', ano: v.ano ?? '', clienteId: v.clienteId ?? '', ativo: v.ativo ?? true })
  }

  function salvarEdicao(){
    if(!editForm.clienteId){ setErro('Selecione um cliente.'); return }
    update.mutate({ id: editId, data:{
      placa: editForm.placa,
      modelo: editForm.modelo || null,
      ano: editForm.ano ? Number(editForm.ano) : null,
      clienteId: editForm.clienteId,
      ativo: editForm.ativo
    }})
  }

  function alternarStatus(v){
    setErro('')
    update.mutate({ id: v.id, data:{
      placa: v.placa,
      modelo: v.modelo || null,
      ano: v.ano ? Number(v.ano) : null,
      clienteId: v.clienteId,
      ativo: !(v.ativo ?? true)
    }})
  }

  return (
    <div>
      <h2>Veículos</h2>

      {erro && <div className="section" style={{background:'#fde8e8', color:'#b91c1c', border:'1px solid #f5c2c2'}}>{erro}</div>}

      <div className="section">
        <div style={{display:'flex', gap:10, alignItems:'center'}}>
          <label>Cliente: </label>
          <select value={clienteId} onChange={e=>{ setClienteId(e.target.value); setForm(f=>({...f, clienteId:e.target.value}))}}>
            {listaClientes.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        </div>
      </div>

      <h3>Novo veículo</h3>
      <div className="section">
        <div className="grid grid-4">
          <input placeholder="Placa" value={form.placa} onChange={e=>setForm({...form, placa:e.target.value})}/>
          <input placeholder="Modelo" value={form.modelo} onChange={e=>setForm({...form, modelo:e.target.value})}/>
          <input placeholder="Ano" value={form.ano} onChange={e=>setForm({...form, ano:e.target.value})}/>
          <button onClick={()=>create.mutate({
            placa: form.placa, modelo: form.modelo, ano: form.ano? Number(form.ano): null, clienteId: form.clienteId || clienteId, ativo: form.ativo
          })}>Salvar</button>
        </div>
      </div>

      <h3 style={{marginTop:16}}>Lista</h3>
      <div className="section">
        {veiculos.isLoading? <p>Carregando...</p> : (
          <table>
            <thead><tr><th>Placa</th><th>Modelo</th><th>Ano</th><th>Cliente</th><th>Status</th><th>Ações</th></tr></thead>
            <tbody>
              {veiculos.data?.map(v=> editId === v.id ? (
                <tr key={v.id}>
                  <td>{v.placa}</td>
                  <td><input value={editForm.modelo} onChange={e=>setEditForm({...editForm, modelo:e.target.value})}/></td>
                  <td><input value={editForm.ano} onChange={e=>setEditForm({...editForm, ano:e.target.value})}/></td>
                  <td>
                    <ClienteCombobox
                      clientes={listaClientes}
                      value={editForm.clienteId}
                      onChange={id=>setEditForm({...editForm, clienteId:id})}
                    />
                  </td>
                  <td>
                    <label style={{display:'flex', alignItems:'center', gap:6}}>
                      <input type="checkbox" checked={editForm.ativo} onChange={e=>setEditForm({...editForm, ativo:e.target.checked})}/> Ativo
                    </label>
                  </td>
                  <td style={{display:'flex', gap:8}}>
                    <button onClick={salvarEdicao} disabled={update.isPending}>Salvar</button>
                    <button className="btn-ghost" onClick={()=>{ setEditId(null); setErro('') }}>Cancelar</button>
                  </td>
                </tr>
              ) : (
                <tr key={v.id}>
                  <td>{v.placa}</td>
                  <td>{v.modelo}</td>
                  <td>{v.ano ?? '-'}</td>
                  <td>{nomeCliente(v.clienteId)}</td>
                  <td>
                    <span className={`badge ${ (v.ativo ?? true) ? 'badge-ativo' : 'badge-inativo' }`}>
                      {(v.ativo ?? true) ? 'Ativo' : 'Inativo'}
                    </span>
                  </td>
                  <td style={{display:'flex', gap:8}}>
                    <button onClick={()=>iniciarEdicao(v)}>Editar</button>
                    <button className="btn-ghost" onClick={()=>alternarStatus(v)}>{(v.ativo ?? true) ? 'Inativar' : 'Ativar'}</button>
                    <button className="btn-ghost" onClick={()=>remover.mutate(v.id)}>Excluir</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
