async function request(path, options) {
  const res = await fetch(path, options)
  if (!res.ok) {
    let message = `Request failed (${res.status})`
    try {
      const body = await res.json()
      if (body?.error) message = body.error
    } catch { /* keep the generic message */ }
    throw new Error(message)
  }
  return res.json()
}

export const api = {
  products: () => request('/api/products'),
  order: (merchantOrderId) => request(`/api/orders/${merchantOrderId}`),
  checkout: (form, lines) =>
    request('/api/orders', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...form, lines }),
    }),
  demoPay: (merchantOrderId, success) =>
    request('/api/dev/demo-pay', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ merchantOrderId, success }),
    }),
}

export const egp = (cents) =>
  `${(cents / 100).toLocaleString('en-EG', { minimumFractionDigits: 2 })} EGP`
