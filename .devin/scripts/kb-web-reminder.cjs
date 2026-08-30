const chunks = [];
process.stdin.on('data', (chunk) => chunks.push(chunk));
process.stdin.on('end', () => {
  let eventName = 'PostToolUse';
  let toolName = '';
  try {
    const payload = JSON.parse(Buffer.concat(chunks).toString('utf8'));
    if (payload.hook_event_name) eventName = payload.hook_event_name;
    toolName = payload.tool_name || '';
  } catch {
    // ignore parse errors
  }

  const context = `You just used ${toolName || 'an external source'}. ` +
    `If the information is useful and reusable for this project, ` +
    `add a concise topic to the circle-knowledge MCP server under the correct parent. ` +
    `Use mcp__circle-knowledge__search_topics first to avoid duplicates.`;

  console.log(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: eventName,
      additionalContext: context
    }
  }));
});
