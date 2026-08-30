const chunks = [];
process.stdin.on('data', (chunk) => chunks.push(chunk));
process.stdin.on('end', () => {
  let eventName = 'UserPromptSubmit';
  try {
    const payload = JSON.parse(Buffer.concat(chunks).toString('utf8'));
    if (payload.hook_event_name) eventName = payload.hook_event_name;
  } catch {
    // ignore parse errors
  }

  const context = `Knowledge base policy (circle-knowledge MCP server):\n` +
    `- Before answering project questions, always query the knowledge base first using ` +
    `mcp__circle-knowledge__search_topics or mcp__circle-knowledge__get_topic.\n` +
    `- If the knowledge base is missing or outdated, update it via ` +
    `mcp__circle-knowledge__add_topic / update_topic.\n` +
    `- When you find reusable info from web_search, webfetch, docs, or other sources, ` +
    `add a concise topic to the knowledge base under the correct parent.\n` +
    `- Prefer the knowledge base over repeating web searches for the same project facts.`;

  console.log(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: eventName,
      additionalContext: context
    }
  }));
});
