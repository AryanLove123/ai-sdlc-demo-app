import { appendFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const MAX_SUMMARY = 500;
const KEYS = ['ts', 'session_id', 'event', 'agent_type', 'tool_name', 'summary'];
const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

const REDACTIONS = [
  [/Bearer\s+[A-Za-z0-9._~+\/=-]+/gi, 'Bearer [REDACTED]'],
  [/("?(?:password|passwd|pwd|token|secret|api[_-]?key|authorization)"?\s*[:=]\s*)("[^"]*"|'[^']*'|[^\s,;]+)/gi, '$1[REDACTED]'],
  [/(ghp_|gho_|github_pat_|sk-)[A-Za-z0-9_-]{8,}/g, '[REDACTED]'],
  [/ChangeMe123!/g, '[REDACTED]'],
];

const redact = (text) =>
  REDACTIONS.reduce((current, [pattern, replacement]) => current.replace(pattern, replacement), text);

const pick = (payload, ...names) => names.map((name) => payload[name]).find((value) => value !== undefined);

function summarize(payload, event) {
  if (event === 'UserPromptSubmit') return pick(payload, 'prompt', 'user_prompt') ?? '';
  if (event === 'PostToolUse') {
    const input = payload.tool_input ?? {};
    return input.filePath ?? input.file_path ?? input.command ?? JSON.stringify(input);
  }
  if (event === 'Stop' || event === 'SubagentStop') return payload.last_assistant_message ?? '';
  return '';
}

const readStdin = () =>
  new Promise((resolve) => {
    let data = '';
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (chunk) => (data += chunk));
    process.stdin.on('end', () => resolve(data));
    process.stdin.on('error', () => resolve(data));
  });

try {
  const payload = JSON.parse((await readStdin()) || '{}');
  const event = pick(payload, 'hookEventName', 'hook_event_name') ?? '';
  const logDirectory = join(repoRoot, 'docs', 'ai-log');
  mkdirSync(logDirectory, { recursive: true });

  if (process.env.AI_LOG_PROBE === '1') {
    appendFileSync(join(logDirectory, '.probe.jsonl'), JSON.stringify({ event, fields: Object.keys(payload) }) + '\n');
  }

  const entry = {
    ts: new Date().toISOString(),
    session_id: pick(payload, 'sessionId', 'session_id') ?? '',
    event,
    agent_type: payload.agent_type ?? 'main',
    tool_name: payload.tool_name ?? '',
    summary: redact(String(summarize(payload, event))).replace(/\s+/g, ' ').trim().slice(0, MAX_SUMMARY),
  };
  appendFileSync(
    join(logDirectory, 'ai-interactions.jsonl'),
    JSON.stringify(Object.fromEntries(KEYS.map((key) => [key, entry[key]]))) + '\n',
  );
} catch {
}
process.exit(0);
