import test from 'node:test'
import assert from 'node:assert/strict'
import { EMPTY_CHAT, reduceChat, reduceTracking } from './proposalLiveModel.js'

const message = (id, isRead = false) => ({ id, proposalId: 7, content: `Message ${id}`, isRead })
const activity = { id: 3, itineraryDayId: 2, status: 'UPCOMING', title: 'Walk' }
const days = [{ id: 2, activitiesList: [activity] }]

test('history resync deduplicates REST and hub echoes and preserves pushed messages', () => {
  let chat = reduceChat(EMPTY_CHAT, 'ReceiveMessage', message(2))
  chat = reduceChat(chat, 'snapshot', { messages: [message(1), message(2)], hasMore: false })
  chat = reduceChat(chat, 'MessageSent', message(2))
  assert.deepEqual(chat.messages.map((m) => m.id), [1, 2])
})

test('a stale history snapshot cannot undo a read receipt', () => {
  let chat = reduceChat(EMPTY_CHAT, 'ReceiveMessage', message(1))
  chat = reduceChat(chat, 'MessageRead', message(1, true))
  chat = reduceChat(chat, 'snapshot', { messages: [message(1)], hasMore: false })
  assert.equal(chat.messages[0].isRead, true)
})

test('read receipt received while loading history can be replayed after snapshot', () => {
  const chat = reduceChat(reduceChat(EMPTY_CHAT, 'snapshot', { messages: [message(1)], hasMore: false }), 'MessageRead', message(1, true))
  assert.equal(chat.messages[0].isRead, true)
})

test('tracking creation is idempotent when both REST and hub deliver the activity', () => {
  const once = reduceTracking(days, 'ActivityCreated', activity)
  assert.equal(reduceTracking(once, 'ActivityCreated', activity)[0].activitiesList.length, 1)
  assert.equal(days[0].activitiesList[0].status, 'UPCOMING')
})

test('tracking uses the exact backend status and updates only its day', () => {
  const result = reduceTracking([...days, { id: 9, activitiesList: [] }], 'ActivityStatusUpdated', { ...activity, status: 'IN_PROGRESS' })
  assert.equal(result[0].activitiesList[0].status, 'IN_PROGRESS')
  assert.equal(result[1].activitiesList.length, 0)
  assert.equal(days[0].activitiesList[0].status, 'UPCOMING')
})

test('reconnect snapshot removes stale activities and a buffered delete remains deleted', () => {
  const snapshot = reduceTracking(days, 'snapshot', [{ id: 2, activitiesList: [{ ...activity, status: 'COMPLETED' }] }])
  const result = reduceTracking(snapshot, 'ActivityDeleted', { activityId: 3 })
  assert.deepEqual(result[0].activitiesList, [])
  assert.deepEqual(reduceTracking(days, 'snapshot', []), [])
})

test('a late HTTP response cannot overwrite a newer broadcast status', () => {
  const latest = [{ id: 2, activitiesList: [{ ...activity, status: 'COMPLETED', updatedAt: '2026-11-02T12:02:00Z' }] }]
  const result = reduceTracking(latest, 'ActivityStatusUpdated', { ...activity, status: 'IN_PROGRESS', updatedAt: '2026-11-02T12:01:00Z' })
  assert.equal(result[0].activitiesList[0].status, 'COMPLETED')
})

test('oldest-first history loads all pages, including messages missed while offline', async () => {
  const { loadMessagePages } = await import('./messageHistory.js')
  const requested = []
  const result = await loadMessagePages(async (page) => {
    requested.push(page)
    const start = (page - 1) * 100 + 1
    return { messages: Array.from({ length: page < 3 ? 100 : 5 }, (_, i) => message(start + i)), hasMore: page < 3 }
  })
  assert.deepEqual(requested, [1, 2, 3])
  assert.equal(result.messages.length, 205)
  assert.equal(result.messages.at(-1).id, 205)
  assert.equal(result.hasMore, false)
})

test('history page failures propagate instead of presenting incomplete history as loaded', async () => {
  const { loadMessagePages } = await import('./messageHistory.js')
  await assert.rejects(loadMessagePages(async (page) => {
    if (page === 2) throw new Error('Disconnected')
    return { messages: [message(1)], hasMore: true }
  }), /Disconnected/)
})
