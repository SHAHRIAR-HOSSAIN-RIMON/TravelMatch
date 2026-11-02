import { api } from './api.js'

async function read(path, token, method = 'get', body) {
  const response = method === 'get'
    ? await api.get(path, { token })
    : await api.post(path, body, { token })
  if (response?.success === false) throw new Error(response.message || 'The request could not be completed.')
  return response
}

export const getGuideOrganizedTrips = (token, page = 1) =>
  read(`/api/GuideApplications/trips?page=${page}&pageSize=20`, token)
export const getGuideOrganizedTrip = (token, id) =>
  read(`/api/GuideApplications/trips/${id}`, token)
export const getGuideApplications = (token, page = 1) =>
  read(`/api/GuideApplications?page=${page}&pageSize=20`, token)
export const applyForOrganizedTrip = (token, id, payload) =>
  read(`/api/GuideApplications/trips/${id}/apply`, token, 'post', payload)
export const getOrganizerApplications = (token, id, page = 1) =>
  read(`/api/OrganizerApplications/trips/${id}/applications?page=${page}&pageSize=20`, token)
export const selectOrganizedTripGuide = (token, id, applicationId) =>
  read(`/api/OrganizerApplications/trips/${id}/applications/${applicationId}/select`, token, 'post')

// Check every page so an older application cannot be submitted twice from the UI.
export async function findMyTripApplication(token, tripId) {
  for (let page = 1; ; page += 1) {
    const result = await getGuideApplications(token, page)
    const application = result.data.find((item) => item.tripId === tripId)
    if (application) return application
    if (page >= result.totalPages) return null
  }
}
