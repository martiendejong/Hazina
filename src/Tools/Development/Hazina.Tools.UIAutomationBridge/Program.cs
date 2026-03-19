using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIAutomationBridge
{
    class Program
    {
        private static HttpListener _listener;
        private static UIA3Automation _automation;
        private static readonly int DefaultPort = 27184;

        static void Main(string[] args)
        {
            int port = args.Length > 0 && int.TryParse(args[0], out int p) ? p : DefaultPort;
            bool debug = args.Contains("--debug") || args.Contains("-d");

            _automation = new UIA3Automation();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");

            try
            {
                _listener.Start();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[UI-BRIDGE] UI Automation Bridge started on http://localhost:{port}");
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Endpoints:");
                Console.ResetColor();
                Console.WriteLine("  GET    /health               - Health check");
                Console.WriteLine("  GET    /windows              - List all windows");
                Console.WriteLine("  GET    /windows/{id}         - Get window details");
                Console.WriteLine("  GET    /windows/{id}/tree    - Get element tree");
                Console.WriteLine("  GET    /windows/{id}/screenshot - Capture screenshot");
                Console.WriteLine("  POST   /click                - Click element");
                Console.WriteLine("  POST   /type                 - Type text");
                Console.WriteLine("  POST   /set-value            - Set element value");
                Console.WriteLine("  POST   /invoke               - Invoke element");
                Console.WriteLine("  POST   /expand               - Expand/collapse element");
                Console.WriteLine("  POST   /select              - Select item");
                Console.WriteLine("  GET    /inspect/{x}/{y}     - Inspect element at coordinates");
                Console.WriteLine("  GET    /docs                - API documentation");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Press CTRL+C to stop");
                Console.ResetColor();
                Console.WriteLine();

                while (_listener.IsListening)
                {
                    var context = _listener.GetContext();
                    try
                    {
                        HandleRequest(context, debug);
                    }
                    catch (Exception ex)
                    {
                        WriteJsonResponse(context, new { ok = false, error = ex.Message }, 500);
                        if (debug)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[ERROR] {ex.Message}");
                            Console.WriteLine(ex.StackTrace);
                            Console.ResetColor();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FATAL] {ex.Message}");
                Console.ResetColor();
                Environment.Exit(1);
            }
            finally
            {
                _automation?.Dispose();
                _listener?.Stop();
            }
        }

        static void HandleRequest(HttpListenerContext context, bool debug)
        {
            var request = context.Request;
            var response = context.Response;
            var method = request.HttpMethod;
            var path = request.Url.AbsolutePath;

            // CORS
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (method == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            if (debug)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {method} {path}");
            }

            // Route handling
            if (path == "/health")
            {
                WriteJsonResponse(context, new { ok = true, message = "UI Automation Bridge is healthy", version = "1.0.0" });
            }
            else if (path == "/windows" && method == "GET")
            {
                HandleGetWindows(context);
            }
            else if (path.StartsWith("/windows/") && path.Split('/').Length == 3 && method == "GET")
            {
                var windowId = path.Split('/')[2];
                HandleGetWindow(context, windowId);
            }
            else if (path.StartsWith("/windows/") && path.EndsWith("/tree") && method == "GET")
            {
                var windowId = path.Split('/')[2];
                HandleGetWindowTree(context, windowId);
            }
            else if (path.StartsWith("/windows/") && path.EndsWith("/screenshot") && method == "GET")
            {
                var windowId = path.Split('/')[2];
                HandleGetScreenshot(context, windowId);
            }
            else if (path == "/click" && method == "POST")
            {
                HandleClick(context);
            }
            else if (path == "/type" && method == "POST")
            {
                HandleType(context);
            }
            else if (path == "/set-value" && method == "POST")
            {
                HandleSetValue(context);
            }
            else if (path == "/invoke" && method == "POST")
            {
                HandleInvoke(context);
            }
            else if (path == "/expand" && method == "POST")
            {
                HandleExpand(context);
            }
            else if (path == "/select" && method == "POST")
            {
                HandleSelect(context);
            }
            else if (path.StartsWith("/inspect/") && method == "GET")
            {
                var parts = path.Split('/');
                if (parts.Length == 4 && int.TryParse(parts[2], out int x) && int.TryParse(parts[3], out int y))
                {
                    HandleInspect(context, x, y);
                }
                else
                {
                    WriteJsonResponse(context, new { ok = false, error = "Invalid coordinates" }, 400);
                }
            }
            else if (path == "/docs")
            {
                HandleDocs(context);
            }
            else
            {
                WriteJsonResponse(context, new { ok = false, error = "Not found" }, 404);
            }
        }

        static void HandleGetWindows(HttpListenerContext context)
        {
            var desktop = _automation.GetDesktop();
            var windows = new List<object>();

            foreach (var w in desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window)))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(w.Name))
                        continue;

                    bool isVisible = true;
                    try { isVisible = !w.IsOffscreen; } catch { }

                    windows.Add(new
                    {
                        id = w.Properties.NativeWindowHandle.ValueOrDefault.ToString(),
                        name = w.Name,
                        automationId = w.Properties.AutomationId.ValueOrDefault,
                        className = w.Properties.ClassName.ValueOrDefault,
                        isVisible,
                        processId = w.Properties.ProcessId.ValueOrDefault,
                        boundingRectangle = new
                        {
                            x = w.BoundingRectangle.X,
                            y = w.BoundingRectangle.Y,
                            width = w.BoundingRectangle.Width,
                            height = w.BoundingRectangle.Height
                        }
                    });
                }
                catch { /* Skip windows that cause errors */ }
            }

            WriteJsonResponse(context, new { ok = true, windows });
        }

        static void HandleGetWindow(HttpListenerContext context, string windowId)
        {
            var window = FindWindow(windowId);
            if (window == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Window not found" }, 404);
                return;
            }

            var windowInfo = new
            {
                id = window.Properties.NativeWindowHandle.ValueOrDefault.ToString(),
                name = window.Name,
                automationId = window.Properties.AutomationId.ValueOrDefault,
                className = window.Properties.ClassName.ValueOrDefault,
                isVisible = !window.IsOffscreen,
                processId = window.Properties.ProcessId.ValueOrDefault,
                boundingRectangle = new
                {
                    x = window.BoundingRectangle.X,
                    y = window.BoundingRectangle.Y,
                    width = window.BoundingRectangle.Width,
                    height = window.BoundingRectangle.Height
                }
            };

            WriteJsonResponse(context, new { ok = true, window = windowInfo });
        }

        static void HandleGetWindowTree(HttpListenerContext context, string windowId)
        {
            var window = FindWindow(windowId);
            if (window == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Window not found" }, 404);
                return;
            }

            var tree = BuildElementTree(window, maxDepth: 5);
            WriteJsonResponse(context, new { ok = true, tree });
        }

        static void HandleGetScreenshot(HttpListenerContext context, string windowId)
        {
            var window = FindWindow(windowId);
            if (window == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Window not found" }, 404);
                return;
            }

            var screenshot = window.Capture();
            using var ms = new MemoryStream();
            screenshot.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            var base64 = Convert.ToBase64String(ms.ToArray());

            WriteJsonResponse(context, new { ok = true, screenshot = $"data:image/png;base64,{base64}" });
        }

        static void HandleClick(HttpListenerContext context)
        {
            var body = GetRequestBody(context.Request);
            var windowId = body["windowId"]?.ToString();
            var automationId = body["automationId"]?.ToString();
            var name = body["name"]?.ToString();
            var className = body["className"]?.ToString();

            var window = FindWindow(windowId);
            if (window == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Window not found" }, 404);
                return;
            }

            var element = FindElement(window, automationId, name, className);
            if (element == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Element not found" }, 404);
                return;
            }

            element.Click();
            WriteJsonResponse(context, new { ok = true, message = "Clicked successfully" });
        }

        static void HandleType(HttpListenerContext context)
        {
            var body = GetRequestBody(context.Request);
            var windowId = body["windowId"]?.ToString();
            var text = body["text"]?.ToString();
            var automationId = body["automationId"]?.ToString();
            var name = body["name"]?.ToString();

            var window = FindWindow(windowId);
            if (window == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Window not found" }, 404);
                return;
            }

            if (automationId != null || name != null)
            {
                var element = FindElement(window, automationId, name, null);
                if (element != null)
                {
                    element.Focus();
                }
            }

            Keyboard.Type(text);
            WriteJsonResponse(context, new { ok = true, message = "Typed successfully" });
        }

        static void HandleSetValue(HttpListenerContext context)
        {
            var body = GetRequestBody(context.Request);
            var windowId = body["windowId"]?.ToString();
            var value = body["value"]?.ToString();
            var automationId = body["automationId"]?.ToString();
            var name = body["name"]?.ToString();

            var window = FindWindow(windowId);
            if (window == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Window not found" }, 404);
                return;
            }

            var element = FindElement(window, automationId, name, null);
            if (element == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Element not found" }, 404);
                return;
            }

            if (element.Patterns.Value.IsSupported)
            {
                element.Patterns.Value.Pattern.SetValue(value);
                WriteJsonResponse(context, new { ok = true, message = "Value set successfully" });
            }
            else
            {
                WriteJsonResponse(context, new { ok = false, error = "Element does not support Value pattern" }, 400);
            }
        }

        static void HandleInvoke(HttpListenerContext context)
        {
            var body = GetRequestBody(context.Request);
            var windowId = body["windowId"]?.ToString();
            var automationId = body["automationId"]?.ToString();
            var name = body["name"]?.ToString();

            var window = FindWindow(windowId);
            if (window == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Window not found" }, 404);
                return;
            }

            var element = FindElement(window, automationId, name, null);
            if (element == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Element not found" }, 404);
                return;
            }

            if (element.Patterns.Invoke.IsSupported)
            {
                element.Patterns.Invoke.Pattern.Invoke();
                WriteJsonResponse(context, new { ok = true, message = "Invoked successfully" });
            }
            else
            {
                WriteJsonResponse(context, new { ok = false, error = "Element does not support Invoke pattern" }, 400);
            }
        }

        static void HandleExpand(HttpListenerContext context)
        {
            var body = GetRequestBody(context.Request);
            var windowId = body["windowId"]?.ToString();
            var automationId = body["automationId"]?.ToString();
            var name = body["name"]?.ToString();
            var expand = body["expand"]?.ToString()?.ToLower() != "false";

            var window = FindWindow(windowId);
            if (window == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Window not found" }, 404);
                return;
            }

            var element = FindElement(window, automationId, name, null);
            if (element == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Element not found" }, 404);
                return;
            }

            if (element.Patterns.ExpandCollapse.IsSupported)
            {
                if (expand)
                    element.Patterns.ExpandCollapse.Pattern.Expand();
                else
                    element.Patterns.ExpandCollapse.Pattern.Collapse();

                WriteJsonResponse(context, new { ok = true, message = expand ? "Expanded" : "Collapsed" });
            }
            else
            {
                WriteJsonResponse(context, new { ok = false, error = "Element does not support ExpandCollapse pattern" }, 400);
            }
        }

        static void HandleSelect(HttpListenerContext context)
        {
            var body = GetRequestBody(context.Request);
            var windowId = body["windowId"]?.ToString();
            var automationId = body["automationId"]?.ToString();
            var name = body["name"]?.ToString();

            var window = FindWindow(windowId);
            if (window == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Window not found" }, 404);
                return;
            }

            var element = FindElement(window, automationId, name, null);
            if (element == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "Element not found" }, 404);
                return;
            }

            if (element.Patterns.SelectionItem.IsSupported)
            {
                element.Patterns.SelectionItem.Pattern.Select();
                WriteJsonResponse(context, new { ok = true, message = "Selected successfully" });
            }
            else
            {
                WriteJsonResponse(context, new { ok = false, error = "Element does not support SelectionItem pattern" }, 400);
            }
        }

        static void HandleInspect(HttpListenerContext context, int x, int y)
        {
            var element = _automation.FromPoint(new System.Drawing.Point(x, y));
            if (element == null)
            {
                WriteJsonResponse(context, new { ok = false, error = "No element found at coordinates" }, 404);
                return;
            }

            var elementInfo = BuildElementInfo(element);
            WriteJsonResponse(context, new { ok = true, element = elementInfo });
        }

        static void HandleDocs(HttpListenerContext context)
        {
            var docs = new
            {
                version = "1.0.0",
                endpoints = new[]
                {
                    new { method = "GET", path = "/health", description = "Health check" },
                    new { method = "GET", path = "/windows", description = "List all windows" },
                    new { method = "GET", path = "/windows/{id}", description = "Get window details" },
                    new { method = "GET", path = "/windows/{id}/tree", description = "Get element tree (max depth 5)" },
                    new { method = "GET", path = "/windows/{id}/screenshot", description = "Capture window screenshot (base64 PNG)" },
                    new { method = "POST", path = "/click", description = "Click element (body: windowId, automationId?, name?, className?)" },
                    new { method = "POST", path = "/type", description = "Type text (body: windowId, text, automationId?, name?)" },
                    new { method = "POST", path = "/set-value", description = "Set element value (body: windowId, value, automationId?, name?)" },
                    new { method = "POST", path = "/invoke", description = "Invoke element (body: windowId, automationId?, name?)" },
                    new { method = "POST", path = "/expand", description = "Expand/collapse element (body: windowId, automationId?, name?, expand)" },
                    new { method = "POST", path = "/select", description = "Select item (body: windowId, automationId?, name?)" },
                    new { method = "GET", path = "/inspect/{x}/{y}", description = "Inspect element at coordinates" },
                    new { method = "GET", path = "/docs", description = "This documentation" }
                }
            };

            WriteJsonResponse(context, docs);
        }

        static Window? FindWindow(string windowId)
        {
            var desktop = _automation.GetDesktop();
            var element = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(w => w.Properties.NativeWindowHandle.ValueOrDefault.ToString() == windowId);
            return element?.AsWindow();
        }

        static AutomationElement? FindElement(AutomationElement parent, string? automationId, string? name, string? className)
        {
            var conditions = new List<Func<FlaUI.Core.Conditions.ConditionFactory, FlaUI.Core.Conditions.ConditionBase>>();

            if (!string.IsNullOrEmpty(automationId))
                conditions.Add(cf => cf.ByAutomationId(automationId));

            if (!string.IsNullOrEmpty(name))
                conditions.Add(cf => cf.ByName(name));

            if (!string.IsNullOrEmpty(className))
                conditions.Add(cf => cf.ByClassName(className));

            if (conditions.Count == 0)
                return null;

            var conditionFactory = new FlaUI.Core.Conditions.ConditionFactory(new UIA3PropertyLibrary());
            FlaUI.Core.Conditions.ConditionBase condition;

            if (conditions.Count == 1)
            {
                condition = conditions[0](conditionFactory);
            }
            else
            {
                var builtConditions = conditions.Select(c => c(conditionFactory)).ToArray();
                condition = builtConditions[0];
                for (int i = 1; i < builtConditions.Length; i++)
                {
                    condition = condition.And(builtConditions[i]);
                }
            }

            return parent.FindFirstDescendant(condition);
        }

        static object BuildElementInfo(AutomationElement element)
        {
            return new
            {
                name = element.Name,
                automationId = element.Properties.AutomationId.ValueOrDefault,
                className = element.Properties.ClassName.ValueOrDefault,
                controlType = element.Properties.ControlType.ValueOrDefault.ToString(),
                isEnabled = element.Properties.IsEnabled.ValueOrDefault,
                isVisible = !element.IsOffscreen,
                boundingRectangle = new
                {
                    x = element.BoundingRectangle.X,
                    y = element.BoundingRectangle.Y,
                    width = element.BoundingRectangle.Width,
                    height = element.BoundingRectangle.Height
                },
                supportedPatterns = new
                {
                    invoke = element.Patterns.Invoke.IsSupported,
                    value = element.Patterns.Value.IsSupported,
                    text = element.Patterns.Text.IsSupported,
                    expandCollapse = element.Patterns.ExpandCollapse.IsSupported,
                    selectionItem = element.Patterns.SelectionItem.IsSupported
                }
            };
        }

        static object BuildElementTree(AutomationElement element, int currentDepth = 0, int maxDepth = 5)
        {
            if (currentDepth >= maxDepth)
                return null;

            var children = element.FindAllChildren()
                .Select(child => BuildElementTree(child, currentDepth + 1, maxDepth))
                .Where(c => c != null)
                .ToList();

            return new
            {
                name = element.Name,
                automationId = element.Properties.AutomationId.ValueOrDefault,
                className = element.Properties.ClassName.ValueOrDefault,
                controlType = element.Properties.ControlType.ValueOrDefault.ToString(),
                children
            };
        }

        static JObject GetRequestBody(HttpListenerRequest request)
        {
            using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
            {
                var body = reader.ReadToEnd();
                return string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
            }
        }

        static void WriteJsonResponse(HttpListenerContext context, object data, int statusCode = 200)
        {
            var json = JsonConvert.SerializeObject(data);
            var buffer = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();
        }
    }
}
