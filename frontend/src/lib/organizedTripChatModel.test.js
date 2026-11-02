import test from 'node:test'
import assert from 'node:assert/strict'

import { normalizeChatHistoryPayload, mergeMessages } from './organizedTripChatModel.js'

test('normalizeChatHistoryPayload keeps valid history payloads intact', () => {
  const payload = {
    title: 'Group trip',
    readOnly: false,
    members: [{ id: 1, name: 'Alice', role: 'Organizer' }],
    messages: [{ id: 11, senderId: 1, senderName: 'Alice', senderRole: 'Organizer', content: 'Hi', sentAt: '2026-11-03T10:00:00Z' }],
    hasMore: false,
  }

  assert.deepEqual(normalizeChatHistoryPayload(payload), payload)
})

test('normalizeChatHistoryPayload rejects unexpected raw text payloads', () => {
  assert.equal(normalizeChatHistoryPayload('<html>bad payload</html>'), null)
  assert.equal(normalizeChatHistoryPayload(''), null)
})

test('mergeMessages keeps a stable message list even with malformed data', () => {
  const merged = mergeMessages(
    [{ id: 1, content: 'older' }],
    [{ id: 2, content: 'newer' }, { id: 2, content: 'duplicate' }],
  )

  assert.deepEqual(merged.map((message) => message.id), [1, 2])
  assert.deepEqual(merged.map((message) => message.content), ['older', 'duplicate'])
})
