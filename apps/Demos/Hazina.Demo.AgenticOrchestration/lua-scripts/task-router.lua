-- task-router.lua
-- Routes an incoming JSON event to a handler based on event.type.
-- Demonstrates the event-driven agent pattern in Lua.

plugin = {
    name        = "task-router",
    version     = "1.0.0",
    description = "Routes JSON events by type — agentic dispatcher pattern"
}

-- -----------------------------------------------------------------------
-- Handlers — add your own here
-- -----------------------------------------------------------------------

local handlers = {}

handlers["ping"] = function(event)
    hazina.log("Handling ping event")
    return { type = "pong", data = { echo = event.data } }
end

handlers["summarize"] = function(event)
    local text = event.data and event.data.text or ""
    hazina.log("Summarize requested for text of length " .. #text)
    -- In a real agent you'd call an LLM here via hazina.* extensions
    return { type = "summary", data = { summary = "Summary of: " .. text } }
end

handlers["unknown"] = function(event)
    hazina.log_warn("Unknown event type: " .. tostring(event.type))
    return { type = "error", data = { message = "Unknown event type: " .. tostring(event.type) } }
end

-- -----------------------------------------------------------------------
-- Entry point
-- -----------------------------------------------------------------------

function execute(ctx)
    local event_json = ctx.params["event"]
    if not event_json then
        return { success = false, message = "Missing 'event' parameter" }
    end

    local event   = json.decode(event_json)
    local handler = handlers[event.type] or handlers["unknown"]
    local result  = handler(event)

    return {
        success = true,
        message = "Routed event type: " .. tostring(event.type),
        data    = result
    }
end
