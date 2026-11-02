/** The server returns oldest-first pages. Fetch through the last page so a
 * reconnect recovers every missed message, even across pagination boundaries. */
export async function loadMessagePages(fetchPage) {
  let page = 1
  let result
  const messages = []
  do {
    result = await fetchPage(page)
    messages.push(...result.messages)
    page += 1
  } while (result.hasMore)
  return { messages, hasMore: false }
}
