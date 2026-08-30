const chunks = [];
process.stdin.on('data', (chunk) => chunks.push(chunk));
process.stdin.on('end', () => {
  let eventName = 'PostToolUse';
  let filePath = '';
  let toolName = '';
  try {
    const payload = JSON.parse(Buffer.concat(chunks).toString('utf8'));
    if (payload.hook_event_name) eventName = payload.hook_event_name;
    toolName = payload.tool_name || '';
    if (payload.tool_input && payload.tool_input.file_path) {
      filePath = payload.tool_input.file_path;
    }
  } catch {
    // ignore parse errors
  }

  const lowerPath = filePath.toLowerCase().replace(/\\/g, '/');
  const ignored = lowerPath.includes('knowledge.json') ||
    lowerPath.includes('.devin/') ||
    lowerPath.includes('mcp-knowledge/') ||
    lowerPath.includes('populate-kb');

  if (ignored) {
    console.log(JSON.stringify({}));
    return;
  }

  const context = `A project file was edited (${toolName}: ${filePath || 'unknown'}). ` +
    `If this change affects architecture, domain models, music algorithms, audio pipeline, ` +
    `UI/UX, ViewModels, build/deployment, or any reusable project knowledge, ` +
    `update the relevant topic in the circle-knowledge MCP server. ` +
    `Do not update the KB for formatting-only changes.`;

  console.log(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: eventName,
      additionalContext: context
    }
  }));
});
