# AI harness (FWO.Ai.Harness)

`roles/ai/files/FWO.Ai.Harness` is an ASP.NET server that runs an AI agent against the FWO MCP server
(`roles/ai/files/FWO.Ai.Mcp`, see [AI MCP server](ai-mcp.md)) and offers it to user interfaces over
[AG-UI](https://docs.ag-ui.com/) (Agent-User Interaction Protocol). It is built on the
[Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) (`Microsoft.Agents.AI`, `ChatClientAgent`,
AG-UI hosting `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore`), which itself is built on
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai). A UI posts 
AG-UI runs with the JWT of the user and receives the answer as server-sent events.

See [Deployment](#deployment) for how the installer deploys it.

| Feature | Provided by |
|---|---|
| UI protocol | AG-UI endpoint `POST /agui` (`MapAGUIServer` of the Agent Framework), answers as server-sent events |
| AI API abstraction | `IChatClient` of Microsoft.Extensions.AI, created from the provider SDKs: `OllamaSharp`, `Microsoft.Extensions.AI.OpenAI`, `Anthropic`, `Google.GenAI` |
| Agent and sessions | One `ChatClientAgent` shared by all users (`FwoAgentFactory`); per request an `FwoUserAgent` with the user's JWT; sessions per user and AG-UI thread in `BoundedAgentSessionStore` |
| Tool management | The tools of the FWO MCP server that the user may call, loaded with the user's JWT for each run (`McpClientTool` of the MCP C# SDK) |
| Prompt management | A run names an MCP prompt in `forwardedProps.prompt` |
| Instructions | The MCP prompt `fwo_agent_instructions`, got with the user's JWT for each run and sent as instructions (system prompt) |
| Agent loop | Function invocation of Microsoft.Extensions.AI, configured by the harness |
| Tool confirmation | AG-UI interrupt for every tool that is not read-only, answered by a resume entry |
| Context compaction | Summary of the older history by the model, triggered by the estimated size of the history |

## AG-UI endpoint

`POST /agui` takes an AG-UI `RunAgentInput` and answers with the AG-UI events of the run (`RUN_STARTED`,
`TEXT_MESSAGE_*`, `TOOL_CALL_*`, `RUN_FINISHED` or `RUN_ERROR`). The request needs `Authorization: Bearer <JWT>` with a
JWT of the middleware server (see `FWO.Ai.Harness.http`):

```json
{
    "threadId": "thread-1",
    "runId": "run-1",
    "messages": [ { "id": "message-1", "role": "user", "content": "Which roles do I have?" } ]
}
```

- **Conversation on the server.** The harness keeps the conversation of each thread, so the next run on the same
  `threadId` continues it. A client only needs to send the new user message. If it sends its whole conversation again,
  as AG-UI clients usually do, the replayed part is ignored: the turn takes only the text of the user messages after the
  last message of another role (`TurnInput`). Everything else a client sends (history, system and developer messages,
  tool calls and results, images, `state`, `context`) is not passed to the model, so a client can neither rewrite the
  stored conversation nor add instructions. A new `threadId` starts a new conversation; a run without `threadId`
  starts one with a new id, which the client reads from `RUN_STARTED`. A user can run on several threads at the same
  time, see [Limits](#limits).
- **Client tools** (`tools` of the input) are not supported; such a run fails with `RUN_ERROR`.
- **Errors** reach the client as `RUN_ERROR` without details; the harness logs them.

### MCP prompts

A run can send a prompt of the MCP server as the user's turn instead of a text:

```json
{
    "threadId": "thread-2",
    "runId": "run-1",
    "messages": [],
    "forwardedProps": {
        "prompt": { "name": "check_access_request_reason", "arguments": { "reason": "..." } }
    }
}
```

The harness gets the prompt from the MCP server with the user's JWT, so the MCP server decides which prompts the user
may get and checks the arguments. Arguments are strings; at most 16 arguments, a name of at most 128 characters and
values of at most 32000 characters are accepted. A run must not send both a prompt and a new text. The prompts a user
may get are listed by the MCP server itself (`prompts/list`), not by the harness.

### Tool confirmation

Every MCP tool that the server does not mark as read-only (`[McpServerTool(ReadOnly = true)]`) requires the
confirmation of the user. Instead of running the tool, the run streams the tool call (`TOOL_CALL_START`, `TOOL_CALL_ARGS`,
`TOOL_CALL_END`) and finishes with an interrupt:

```json
{ "type": "RUN_FINISHED", "outcome": { "type": "interrupt", "interrupts": [ { "id": "<interrupt id>", "reason": "tool_call", "toolCallId": "<call id>" } ] } }
```

The client asks the user and answers every interrupt in the next run on the same thread:

```json
{
    "threadId": "thread-1",
    "runId": "run-2",
    "messages": [],
    "resume": [ { "interruptId": "<interrupt id>", "payload": { "approved": true, "toolCall": { "callId": "<call id>", "name": "<tool>", "arguments": { } } } } ]
}
```

A rejected tool is not run. The Agent Framework records the approval requests in the session and only honors an answer
to a request it recorded: an answer to an unknown interrupt is ignored, and an approved call always runs with the tool
and arguments the user was asked about, whatever `toolCall` the client sends. An interrupt that is not answered in the
next run is rejected. A new tool that changes data is thus confirmed even if nobody remembers to configure it (fail
closed).

### Limits

- Only JWTs of users holding an FWO role that is neither technical nor anonymous and carrying the user's directory
  identity (`x-hasura-uuid`) are accepted, like by the MCP server (401 without, 403 otherwise).
- `RunsPerMinute` (default 10) runs per user within any minute; more are rejected with 429 and `Retry-After`. Each
  approval answer is a run.
- `MaxConcurrentRunsPerUser` (default 3) runs per user at a time, each on a thread of its own; one more is rejected
  with 429 right away.
- One run per thread at a time (`SingleRunPerThreadFilter`): a run on a thread whose previous run is still running is
  rejected with 409 right away. The thread stays locked from loading its session until the session is stored after
  the run, so two runs never start from the same stored session (which would lose history or run an approved tool
  twice). It is released before the response ends, so a client can start the next run on the thread, e.g. to answer
  an interrupt, as soon as it has read the events of the previous one. A run without `threadId` gets its new id before
  the lock is taken, so its thread is locked as well.
- Request bodies of at most 1 MiB (413 otherwise), and at most 32000 characters of new text per turn.

## Sessions

- The sessions are kept in memory (`BoundedAgentSessionStore`) and are lost when the harness server restarts. Each user
  has their own sessions: the AG-UI hosting puts the user's directory identity into the session key, so another user
  sending the same `threadId` gets a conversation of their own.
- `MaxSessionsPerUser` (default 10) conversations are kept per user; storing one more drops the least recently used one.
  A conversation not used for `SessionIdleTimeout` (default 4 hours) is dropped.
- The sessions contain the tool results, i.e. firewall data.
- A failed or cancelled turn leaves the history unchanged, apart from a compaction done before the turn started (see
  below).

## Agent loop

The loop is the function invocation of Microsoft.Extensions.AI, configured by the harness:

- `MaxIterationsPerRequest` (default 10) limits the model requests per user turn. If the model still calls tools in the
  last of them, it is asked once more without tools for its answer. A tool call it makes there gets no result and is
  not stored in the history.
- `MaxConsecutiveErrorsPerRequest` (default 3) is the number of failed tool call rounds in a row before the turn fails.
- Exception details of failed tools are not passed to the model.
- Tools are not called concurrently.
- Each run connects to the MCP server with the JWT of its request and loads the user's tools and the instructions; the
  connection is closed when the run ends.
- The instructions are the text of the MCP prompt `fwo_agent_instructions` (`FwoUserAgent.kInstructionsPromptName`,
  keep it in step with the MCP server). They are sent with every request of the run and are not stored in the history.
  A run fails if the MCP server does not offer the prompt.

## Context compaction

Before each turn, the harness checks the stored history. If its estimated size exceeds
`ContextWindowTokens - CompactionReserveTokens`, all older messages are summarized by the same model in one call
(without tools):

- The 4 most recent message groups stay verbatim. A tool call is never separated from its result. Nothing else is
  dropped.
- The summary is stored as a user message starting with `[Summary]`, at the start of the history. The Agent Framework
  writes it as an assistant message, but providers expect a history to start with a user message, and Gemini rejects a
  tool call right after a model message.
- Later summaries include the previous summary.
- Tokens are estimated (UTF-8 bytes / 4). The instructions and the tool schemas are not counted, so the reserve must
  hold one full turn (instructions, tool schemas, the new message, the tool results of that turn and the answer) as well
  as the summary call.
- If the summary call fails, the history stays as it is and the next turn tries again.
- The summary prompt asks to keep names, UIDs, IP addresses, networks and ports exactly.

The compaction and session store types are experimental in the Agent Framework (diagnostic `MAAI001`, suppressed in
the project file). The AG-UI hosting packages are only published as previews.

## Settings

The settings are read from the configuration section `Harness`: environment variables (e.g.
`Harness__Provider__ApiKey`) and the command line. The API key must never be put into a file of the sources. The server refuses to start with invalid server settings (the settings below other than `Provider` and `Agent`). The
`Provider` and `Agent` settings are checked when the agent factory is created, which is done by the first run, not at
startup: the server starts without a configured AI provider, and every run fails with the exception of the factory
(HTTP 500, logged by the harness) until one is configured.

- `McpEndpoint` (default `http://127.0.0.1:8890/mcp`, the MCP server on the same host), `McpConnectionTimeout`
  (optional), `RunsPerMinute`, `MaxSessionsPerUser`, `MaxConcurrentRunsPerUser` (at most `MaxSessionsPerUser`, as
  more runs would drop each other's conversations) and `SessionIdleTimeout`, see above.
- `Provider` (`AiProviderSettings`): `Provider` (`Ollama`, `OpenAi`, `Anthropic`, `Google`), `Model`, `ApiKey`
  (required except for Ollama), `Endpoint` (optional base URL, e.g. an OpenAI compatible server; Ollama defaults to
  `http://localhost:11434`), `MaxOutputTokens` (optional, must be smaller than the context window) and
  `ContextWindowTokens` (required: the context window of the model).
- `Agent` (`AgentSettings`): `MaxIterationsPerRequest`, `MaxConsecutiveErrorsPerRequest` and `CompactionReserveTokens`
  (optional, default a quarter of the context window; must be smaller than the context window and larger than
  `MaxOutputTokens` if that is set)

The JWT public key is read from the FWO config file (`ConfigFile.JwtPublicKey`), like by the MCP server.

## Security

- The MCP server is called with the JWT of the request, so the tools are subject to that user's roles and permissions.
  The harness adds no rights of its own.
- The AG-UI input of a client never reaches the AI provider: the agent passes only the new user input and the user's
  tools on.
- A JWT or an API key is only sent over https, or over http to a loopback address.
- Exception details of failed tool calls are not passed on to the model, and run errors not to the client.
- Tools that are not marked read-only need the confirmation of the user before they run; the confirmation is bound to
  the request the agent recorded.
- Tool results and the replies of the model are untrusted data. The summary is written by the same model and replaces
  the summarized messages, so text injected into them, e.g. through a tool result, can carry over into it.

## Deployment

The installer deploys the harness with the MCP server (`install_ai_server`, see
[install-advanced.md](../../installer/install-advanced.md)): `roles/ai` publishes `FWO.Ai.Harness` on the middleware
host and runs it as the service `fworch-ai-harness` (`roles/ai/templates/fworch-ai-harness.service.j2`).

- It listens on `ai_harness_native_uri` (loopback, port `ai_harness_internal_port`, default 8891) and the Apache vhost
  of the middleware publishes the AG-UI endpoint under the same path (`ai_harness_agui_path`, `/agui`):
  `https://<middleware host>:8888/agui`. Keep `ai_harness_agui_path` in step with `HarnessServerSetup.kAgUiPath`.
- The unit sets `McpEndpoint` to `ai_mcp_native_mcp_uri` (`Harness__McpEndpoint`), the MCP server the installer deploys.
- The installer does not configure the AI provider (`Provider`), so runs fail until one is configured, see
  [Settings](#settings).
- Like the MCP server, the unit hides the JWT signing key of the middleware (`InaccessiblePaths`).
- An uninstallation stops the service and removes its unit and files. Setting `install_ai_server=no` on an existing
  installation only removes `/agui` from the Apache vhost.

## Tests

The unit tests are in `roles/tests-unit/files/FWO.Ai.Tests`. They run the harness against the MCP server pipeline on a
test server, with a scripted chat client instead of a real AI provider, both directly (`FwoUserAgent`) and through the
AG-UI endpoint on a second test server (`HarnessServerTest`). Because they reference the harness sources, the installer
copies `FWO.Ai.Harness` to the middleware host next to `FWO.Ai.Mcp` (`roles/ai`).

The integration test `roles/tests-integration/tasks/test-ai-harness.yml` checks the installed service: its loopback
listener, the hidden JWT signing key, and that the harness and its Apache endpoint refuse runs without a token (401) and
of a user without an FWO role (403). It never sends a run of an FWO user, as that would reach the AI provider once one
is configured.
