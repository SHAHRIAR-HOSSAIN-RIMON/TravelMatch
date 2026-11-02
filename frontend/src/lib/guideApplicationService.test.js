import test from 'node:test'
import assert from 'node:assert/strict'
import { registerHooks } from 'node:module'

// Exercise the real API client (including authorization and JSON serialization)
// without creating applications or selecting guides in a live database.
test('organized trip application API contracts', async (t) => {
  // Vite normally injects this build-time environment value in the API client.
  const hooks = registerHooks({
    load(url, context, nextLoad) {
      const result = nextLoad(url, context)
      if (url.endsWith('/api.js')) {
        return { ...result, source: String(result.source).replace('import.meta.env.VITE_API_BASE_URL', "''") }
      }
      return result
    },
  })
  const originalFetch = globalThis.fetch
  try {
    const service = await import('./guideApplicationService.js')
    let requests = []
    let respond = () => ({ success: true, data: [], totalPages: 0 })
    globalThis.fetch = async (url, options) => {
      requests.push({ url, ...options })
      return new Response(JSON.stringify(respond(url)), { headers: { 'Content-Type': 'application/json' } })
    }
    await t.test('browses guide trips with pagination and authentication', async () => {
      await service.getGuideOrganizedTrips('guide-token', 2)
      assert.equal(requests.at(-1).url, '/api/GuideApplications/trips?page=2&pageSize=20')
      assert.equal(requests.at(-1).headers.Authorization, 'Bearer guide-token')
    })
    await t.test('finds previous applications beyond the first page', async () => {
      requests = []
      respond = (url) => ({ success: true, totalPages: 2, data: url.includes('page=2&') ? [{ id: 32, tripId: 7, status: 'Pending' }] : [{ id: 1, tripId: 8 }] })
      assert.equal((await service.findMyTripApplication('guide-token', 7)).id, 32)
      assert.equal(requests.length, 2)
      assert.equal(await service.findMyTripApplication('guide-token', 99), null)
    })
    await t.test('submits the guide quote and selects through the organizer endpoint', async () => {
      respond = () => ({ success: true, data: { id: 32 } })
      await service.applyForOrganizedTrip('guide-token', 7, { proposedPrice: 1250, message: 'Available for these dates.' })
      assert.equal(requests.at(-1).url, '/api/GuideApplications/trips/7/apply')
      assert.equal(requests.at(-1).method, 'POST')
      assert.deepEqual(JSON.parse(requests.at(-1).body), { proposedPrice: 1250, message: 'Available for these dates.' })
      await service.getOrganizerApplications('organizer-token', 7, 2)
      assert.equal(requests.at(-1).url, '/api/OrganizerApplications/trips/7/applications?page=2&pageSize=20')
      await service.selectOrganizedTripGuide('organizer-token', 7, 32)
      assert.equal(requests.at(-1).url, '/api/OrganizerApplications/trips/7/applications/32/select')
      assert.equal(requests.at(-1).headers.Authorization, 'Bearer organizer-token')
      assert.equal(requests.at(-1).method, 'POST')
    })
    await t.test('propagates failed application responses instead of reporting success', async () => {
      respond = () => ({ success: false, message: 'You have already applied.' })
      await assert.rejects(service.applyForOrganizedTrip('guide-token', 7, {}), /already applied/)
    })
  } finally {
    globalThis.fetch = originalFetch
    hooks.deregister()
  }
})
