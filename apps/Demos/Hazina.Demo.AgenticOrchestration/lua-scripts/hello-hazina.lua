-- hello-hazina.lua
-- Minimal Lua plugin. Demonstrates hazina.log and basic parameter access.

plugin = {
    name        = "hello-hazina",
    version     = "1.0.0",
    description = "Greeting plugin — demonstrates the Hazina Lua API"
}

function execute(ctx)
    local name = ctx.params["name"] or "World"
    hazina.log("Hello from Lua! name=" .. name)

    return {
        success = true,
        message = "Greeting sent",
        data    = { greeting = "Hello, " .. name .. "! — from Lua" }
    }
end
