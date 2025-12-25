# DynamicAPI Tools in Windows App

De Windows App heeft nu automatisch toegang tot de DynamicAPI tools. Deze tools worden automatisch aan alle agents toegevoegd wanneer je een chat opent.

## Beschikbare Tools

Alle agents in de Windows App hebben nu toegang tot deze 3 tools:

### 1. `web_search` - Zoek informatie op internet
Zoek naar API documentatie, tutorials, of andere informatie.

**Parameters:**
- `query` (string, verplicht): Zoekterm
- `count` (number, optioneel): Aantal resultaten (standaard: 5)

**Voorbeeld:**
```
Agent: Ik ga API documentatie zoeken
Tool: web_search
Input: {"query": "Stripe API create payment", "count": 10}
```

### 2. `fetch_url` - Haal content op van een URL
Lees documentatie, webpagina's of API responses.

**Parameters:**
- `url` (string, verplicht): URL om op te halen

**Voorbeeld:**
```
Tool: fetch_url
Input: {"url": "https://stripe.com/docs/api/payment_intents"}
```

### 3. `api_call` - Roep elke API aan
Maak HTTP requests naar elke API endpoint.

**Parameters:**
- `url` (string, verplicht): API endpoint URL
- `method` (string, optioneel): GET, POST, PUT, DELETE, PATCH (standaard: GET)
- `service_name` (string, optioneel): Naam van service voor credentials
- `headers` (object, optioneel): Custom HTTP headers
- `query_params` (object, optioneel): URL query parameters
- `body` (object, optioneel): Request body voor POST/PUT/PATCH
- `auth_type` (string, optioneel): none, bearer, api_key, basic, oauth2

**Voorbeeld:**
```
Tool: api_call
Input: {
  "url": "https://api.stripe.com/v1/customers",
  "method": "GET",
  "service_name": "stripe"
}
```

## Credentials Configureren

API credentials worden opgeslagen in: `<App Directory>/credentials/`

### Voorbeeld: Stripe API Key

1. Maak bestand: `credentials/stripe.json`
2. Inhoud:
```json
{
  "api_key": "sk_test_YOUR_KEY_HERE"
}
```

### Voorbeeld: Bing Search API Key (voor web_search)

1. Maak bestand: `credentials/bing.json`
2. Inhoud:
```json
{
  "api_key": "YOUR_BING_API_KEY"
}
```

### Andere Services

Voor andere APIs maak je hetzelfde patroon:
- `credentials/google.json`
- `credentials/github.json`
- `credentials/openai.json`
- etc.

## Voorbeeld Workflows

### Workflow 1: API Documentatie Zoeken en Gebruiken

```
User: "Haal mijn laatste Stripe betalingen op"

Agent workflow:
1. web_search("Stripe API list payments")
   → Vindt documentatie URL
2. fetch_url("https://stripe.com/docs/api/charges/list")
   → Leest hoe de API werkt
3. api_call({
     "url": "https://api.stripe.com/v1/charges",
     "method": "GET",
     "service_name": "stripe"
   })
   → Haalt betalingen op met automatische authenticatie
```

### Workflow 2: Nieuwe API Integreren Zonder Code

```
User: "Maak een GitHub issue aan in mijn repo"

Agent workflow:
1. web_search("GitHub API create issue")
2. fetch_url(found documentation)
3. api_call({
     "url": "https://api.github.com/repos/user/repo/issues",
     "method": "POST",
     "service_name": "github",
     "body": {
       "title": "New issue",
       "body": "Issue description"
     }
   })
```

## Voordelen

✅ **Geen code wijzigingen nodig** - Agents kunnen elke API gebruiken zonder dat je code hoeft te schrijven
✅ **Automatische authenticatie** - Credentials worden automatisch geïnjecteerd
✅ **Self-service documentatie** - Agents kunnen zelf API docs opzoeken
✅ **Flexibel** - Werkt met elke HTTP API

## Beveiliging

⚠️ **LET OP:**
- Voeg `credentials/` toe aan `.gitignore`
- Gebruik NOOIT production API keys in development
- Gebruik waar mogelijk read-only API keys
- Check regelmatig welke credentials er opgeslagen zijn

## Troubleshooting

### "Bing Search API key not found"
De `web_search` tool heeft een Bing API key nodig. Maak `credentials/bing.json` aan met je API key.
Krijg een key: https://www.microsoft.com/en-us/bing/apis/bing-web-search-api

### "Credential not found: stripe/api_key"
De agent probeert een API aan te roepen maar kan de credentials niet vinden.
Maak `credentials/stripe.json` aan met de juiste credentials.

### Tools worden niet herkend
De tools worden automatisch toegevoegd bij het openen van een chat. Als ze niet werken:
1. Sluit de chat window
2. Open opnieuw
3. Check of `credentials/` directory bestaat in de app directory

## Meer Informatie

Zie ook:
- `Hazina.DynamicAPI/README.md` - Volledige technische documentatie
- `Hazina.DynamicAPI/USAGE_GUIDE.md` - Nederlandse usage guide
- `Hazina.DynamicAPI/Examples/` - Code voorbeelden
