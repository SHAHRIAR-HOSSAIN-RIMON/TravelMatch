export function normalizeChatHistoryPayload(payload) {
  if (!payload || typeof payload !== 'object' || Array.isArray(payload)) {
    return null
  }

  const title = typeof payload.title === 'string' ? payload.title : ''
  const readOnly = Boolean(payload.readOnly)
  const hasMore = Boolean(payload.hasMore)
  const members = Array.isArray(payload.members)
    ? payload.members.filter((member) => member && typeof member === 'object')
    : []
  const messages = Array.isArray(payload.messages)
    ? payload.messages.filter((message) => message && typeof message === 'object')
    : []

  return {
    title,
    readOnly,
    hasMore,
    members: members.map((member) => ({
      id: Number(member.id ?? 0),
      name: String(member.name ?? 'Unknown guest'),
      role: String(member.role ?? 'Tourist'),
    })),
    messages: messages.map((message) => ({
      id: Number(message.id ?? 0),
      senderId: Number(message.senderId ?? 0),
      senderName: String(message.senderName ?? 'Unknown sender'),
      senderRole: String(message.senderRole ?? 'Tourist'),
      content: String(message.content ?? ''),
      sentAt: message.sentAt ?? new Date().toISOString(),
    })),
  }
}

export function mergeMessages(left = [], right = []) {
  const normalizedLeft = Array.isArray(left) ? left.filter(Boolean) : []
  const normalizedRight = Array.isArray(right) ? right.filter(Boolean) : []

  return [...new Map([...normalizedLeft, ...normalizedRight].map((message) => [message.id, message])).values()]
    .filter((message) => message && typeof message === 'object')
    .sort((a, b) => Number(a.id ?? 0) - Number(b.id ?? 0))
}
