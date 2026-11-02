export const CHAT_EVENTS = ['ReceiveMessage', 'MessageSent', 'MessageRead']
export const TRACKING_EVENTS = ['ActivityStatusUpdated', 'ActivityCreated', 'ActivityDeleted']
export const EMPTY_CHAT = { messages: [], hasMore: false }

export function mergeMessages(previous, incoming) {
  const byId = new Map(previous.map((message) => [message.id, message]))
  incoming.forEach((message) => {
    const old = byId.get(message.id)
    byId.set(message.id, { ...old, ...message, isRead: Boolean(old?.isRead || message.isRead) })
  })
  return [...byId.values()].sort((a, b) => a.id - b.id)
}

export function reduceChat(previous, event, payload) {
  if (event === 'snapshot' || event === 'older') {
    const keepOlderCursor = event === 'snapshot' && previous.messages.length > 0 &&
      payload.messages.length > 0 && previous.messages[0].id < payload.messages[0].id
    return {
      messages: mergeMessages(previous.messages, payload.messages),
      hasMore: keepOlderCursor ? previous.hasMore : payload.hasMore,
    }
  }
  if (event === 'MessageRead') {
    return { ...previous, messages: previous.messages.map((m) => m.id === payload.id ? { ...m, isRead: true } : m) }
  }
  return { ...previous, messages: mergeMessages(previous.messages, [payload]) }
}

export function reduceTracking(previous, event, payload) {
  if (event === 'snapshot') return payload
  if (event === 'ActivityDeleted') {
    return previous.map((day) => ({ ...day, activitiesList: day.activitiesList.filter((a) => a.id !== payload.activityId) }))
  }
  return previous.map((day) => day.id !== payload.itineraryDayId ? day : {
    ...day,
    activitiesList: day.activitiesList.some((a) => a.id === payload.id)
      ? day.activitiesList.map((a) => a.id === payload.id && !(a.updatedAt && payload.updatedAt && Date.parse(a.updatedAt) > Date.parse(payload.updatedAt)) ? { ...a, ...payload } : a)
      : [...day.activitiesList, payload],
  })
}
