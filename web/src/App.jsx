import { useEffect, useMemo, useState } from 'react'
import { api, egp } from './api.js'

function useHashRoute() {
  const [hash, setHash] = useState(window.location.hash)
  useEffect(() => {
    const onChange = () => setHash(window.location.hash)
    window.addEventListener('hashchange', onChange)
    return () => window.removeEventListener('hashchange', onChange)
  }, [])
  return hash.replace(/^#/, '') || '/'
}

function useCart() {
  const [lines, setLines] = useState(() => JSON.parse(localStorage.getItem('khan-cart') || '[]'))
  useEffect(() => localStorage.setItem('khan-cart', JSON.stringify(lines)), [lines])
  return {
    lines,
    add: (productId) =>
      setLines((ls) => {
        const found = ls.find((l) => l.productId === productId)
        return found
          ? ls.map((l) => (l.productId === productId ? { ...l, quantity: l.quantity + 1 } : l))
          : [...ls, { productId, quantity: 1 }]
      }),
    remove: (productId) => setLines((ls) => ls.filter((l) => l.productId !== productId)),
    setQuantity: (productId, quantity) =>
      setLines((ls) => (quantity <= 0 ? ls.filter((l) => l.productId !== productId) : ls.map((l) => (l.productId === productId ? { ...l, quantity } : l)))),
    clear: () => setLines([]),
  }
}

export default function App() {
  const route = useHashRoute()
  const cart = useCart()
  const [products, setProducts] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    api.products().then(setProducts).catch((e) => setError(e.message))
  }, [])

  if (route.startsWith('/pay/demo')) return <DemoPayPage route={route} />
  if (route.startsWith('/order/')) return <OrderPage merchantOrderId={route.slice('/order/'.length)} />

  return (
    <div className="shell">
      <header>
        <h1>
          خان <span className="latin">Khan</span>
        </h1>
        <p className="tagline">A taste of the bazaar, delivered. Payments by Paymob.</p>
      </header>
      {error && <p className="error">{error}</p>}
      {route === '/checkout' ? (
        <CheckoutPage cart={cart} products={products} />
      ) : (
        <>
          {!products ? (
            <p>Loading the shelves…</p>
          ) : (
            <main className="grid">
              {products.map((p) => (
                <article key={p.id} className="card">
                  <div className="names">
                    <h2>{p.name}</h2>
                    <h3>{p.nameAr}</h3>
                  </div>
                  <p>{p.description}</p>
                  <footer>
                    <strong>{egp(p.priceCents)}</strong>
                    <span className={p.stock > 0 ? 'stock' : 'stock out'}>
                      {p.stock > 0 ? `${p.stock} in stock` : 'sold out'}
                    </span>
                    <button disabled={p.stock === 0} onClick={() => cart.add(p.id)}>
                      Add to cart
                    </button>
                  </footer>
                </article>
              ))}
            </main>
          )}
          <CartBar cart={cart} products={products} />
        </>
      )}
    </div>
  )
}

function CartBar({ cart, products }) {
  if (!products || cart.lines.length === 0) return null
  const count = cart.lines.reduce((n, l) => n + l.quantity, 0)
  const total = cart.lines.reduce((sum, l) => {
    const p = products.find((p) => p.id === l.productId)
    return sum + (p ? p.priceCents * l.quantity : 0)
  }, 0)
  return (
    <nav className="cartbar">
      <span>
        {count} item{count > 1 ? 's' : ''} · {egp(total)}
      </span>
      <a href="#/checkout">Checkout →</a>
    </nav>
  )
}

function CheckoutPage({ cart, products }) {
  const [form, setForm] = useState({ customerName: '', email: '', phone: '', city: 'Cairo' })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const lines = cart.lines
  const detailed = useMemo(
    () =>
      lines
        .map((l) => ({ ...l, product: products?.find((p) => p.id === l.productId) }))
        .filter((l) => l.product),
    [lines, products],
  )
  const total = detailed.reduce((sum, l) => sum + l.product.priceCents * l.quantity, 0)

  async function submit(e) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const order = await api.checkout(form, lines)
      cart.clear()
      // Send the customer to the payment page (Paymob iframe in production,
      // the demo checkout when no keys are configured).
      const target = new URL(order.paymentRedirectUrl, window.location.origin)
      window.location.hash = target.pathname + target.search
    } catch (err) {
      setError(err.message)
      setBusy(false)
    }
  }

  if (detailed.length === 0)
    return (
      <main>
        <p>Your cart is empty.</p>
        <a href="#/">← Back to the shelves</a>
      </main>
    )

  return (
    <main className="checkout">
      <h2>Checkout</h2>
      <ul>
        {detailed.map((l) => (
          <li key={l.productId}>
            <input
              type="number"
              min="0"
              value={l.quantity}
              onChange={(e) => cart.setQuantity(l.productId, Number(e.target.value))}
            />
            × {l.product.name} <span>{egp(l.product.priceCents * l.quantity)}</span>
            <button className="link" onClick={() => cart.remove(l.productId)}>
              remove
            </button>
          </li>
        ))}
      </ul>
      <p className="total">Total: {egp(total)}</p>
      <form onSubmit={submit}>
        <label>
          Name
          <input required value={form.customerName} onChange={(e) => setForm({ ...form, customerName: e.target.value })} />
        </label>
        <label>
          Email
          <input required type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
        </label>
        <label>
          Phone
          <input required value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
        </label>
        <label>
          City
          <input required value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} />
        </label>
        {error && <p className="error">{error}</p>}
        <button disabled={busy}>{busy ? 'Placing the order…' : `Pay ${egp(total)}`}</button>
      </form>
      <a href="#/">← Keep shopping</a>
    </main>
  )
}

function DemoPayPage({ route }) {
  const params = new URLSearchParams(route.split('?')[1])
  const merchantOrderId = params.get('order')
  const amount = Number(params.get('amount') || 0)
  const [done, setDone] = useState(null)
  const [error, setError] = useState(null)

  async function pay(success) {
    try {
      await api.demoPay(merchantOrderId, success)
      setDone(success ? 'paid' : 'failed')
    } catch (e) {
      setError(e.message)
    }
  }

  return (
    <div className="shell narrow">
      <header>
        <h1>Demo checkout</h1>
        <p className="tagline">Stands in for the Paymob iframe when the app runs without live keys.</p>
      </header>
      <main>
        {done ? (
          <p>
            Payment {done}. <a href={`#/order/${merchantOrderId}`}>See the order →</a>
          </p>
        ) : (
          <>
            <p>
              Order <code>{merchantOrderId}</code> · {egp(amount)}
            </p>
            {error && <p className="error">{error}</p>}
            <button onClick={() => pay(true)}>Pay (succeeds)</button>{' '}
            <button className="secondary" onClick={() => pay(false)}>
              Decline (fails)
            </button>
          </>
        )}
      </main>
    </div>
  )
}

function OrderPage({ merchantOrderId }) {
  const [order, setOrder] = useState(null)
  const [error, setError] = useState(null)
  useEffect(() => {
    api.order(merchantOrderId).then(setOrder).catch((e) => setError(e.message))
  }, [merchantOrderId])

  return (
    <div className="shell narrow">
      <header>
        <h1>Order {merchantOrderId}</h1>
      </header>
      <main>
        {error && <p className="error">{error}</p>}
        {!order ? (
          !error && <p>Loading…</p>
        ) : (
          <>
            <p className={`status ${order.status.toLowerCase()}`}>{order.status}</p>
            <ul>
              {order.items.map((i) => (
                <li key={i.productName}>
                  {i.quantity} × {i.productName} <span>{egp(i.unitPriceCents * i.quantity)}</span>
                </li>
              ))}
            </ul>
            <p className="total">Total: {egp(order.totalCents)}</p>
            <a href="#/">← Back to the store</a>
          </>
        )}
      </main>
    </div>
  )
}
