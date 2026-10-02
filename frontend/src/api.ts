export interface Section { label: string; text: string }
export interface Source {
  ref: number
  chunkId: string
  title: string
  category: string
  subCategory: string
  tags: string[]
  score: number
  sections: Section[]
}
export interface ChatResponse { answer: string; sources: Source[]; groundedInDatabase: boolean }
export interface Turn { role: 'user' | 'assistant'; text: string }
export interface ChatMsg { role: 'user' | 'assistant'; text: string; question?: string; sources?: Source[]; error?: boolean }
export interface EmailItem { question: string; answer: string; sources: Source[] }

async function errorMessage(res: Response, fallback: string): Promise<string> {
  const body = await res.json().catch(() => null)
  return body?.error ?? (res.status === 429 ? 'Demasiadas solicitudes. Inténtalo de nuevo más tarde.' : fallback)
}

export async function ask(message: string, history: Turn[]): Promise<ChatResponse> {
  const res = await fetch('/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message, history })
  })
  if (!res.ok) throw new Error(await errorMessage(res, 'No se pudo completar la consulta. Inténtalo de nuevo.'))
  return res.json()
}

export async function getFeatures(): Promise<{ email: boolean }> {
  try {
    const res = await fetch('/api/features')
    return res.ok ? await res.json() : { email: false }
  } catch {
    return { email: false }
  }
}

export async function sendEmail(to: string, items: EmailItem[]): Promise<void> {
  const res = await fetch('/api/email', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ to, items })
  })
  if (!res.ok) throw new Error(await errorMessage(res, 'No se pudo enviar el correo.'))
}
