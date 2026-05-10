-- echo-agent.lua
-- Echoes the input back as JSON. Good smoke-test for the json.encode / json.decode helpers.

plugin = {
    name        = "echo-agent",
    version     = "1.0.0",
    description = "Echoes input parameters back as a JSON string"
}

function execute(ctx)
    hazina.log("echo-agent received params")

    -- Encode the entire params table to JSON and back, proving round-trip works
    local encoded = json.encode(ctx.params)
    hazina.log("params as JSON: " .. encoded)

    local decoded = json.decode(encoded)

    return {
        success = true,
        message = "Echo complete",
        data    = {
            echo   = decoded,
            raw    = encoded
        }
    }
end
