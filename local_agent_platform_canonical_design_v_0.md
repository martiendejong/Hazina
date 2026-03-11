# Local Agent Platform – Canonical Design v0.1

> **Status:** Draft (canonical)
>
> **Doel:** Dit document is de *single source of truth* voor het ontwerp van het Local Agent Platform. Alle architectuur-, UX- en veiligheidsbeslissingen worden hieraan getoetst.

---

## 1. Visie & Afbakening

### 1.1 Probleemdefinitie
Bestaande AI- en automatiseringsoplossingen falen voor normale gebruikers doordat ze:
- cloud-first zijn (data verlaat het systeem),
- terminal- of developer-centric zijn,
- onvoldoende transparantie en controle bieden,
- óf neerkomen op onveilige remote desktop-oplossingen.

### 1.2 Doel van dit systeem
Het systeem biedt een **local-first, intent-based agent** die:
- lokaal uitvoert,
- continu uitlegbaar is,
- visueel inzicht geeft in wat er gebeurt,
- en veilig op afstand bedienbaar *kan worden*.

### 1.3 Wat dit systeem expliciet **niet** is
- Geen remote desktop
- Geen autonome cloud-agent
- Geen enterprise endpoint management tool
- Geen IDE of terminal-gedreven developer tool

---

## 2. Gebruikers & Gebruiksscenario’s

### 2.1 Primaire gebruiker
De normale gebruiker die:
- geen developer is,
- controle wil zonder technische details,
- wil zien wat er gebeurt voordat iets verandert.

### 2.2 Secundaire gebruiker
De power user die:
- complexere taken uitvoert,
- inzicht wil in structuur en status,
- maar nog steeds visuele controle verkiest boven terminalgebruik.

### 2.3 Kernscenario’s
1. Begrijpen wat er op het systeem staat
2. Een taak uitvoeren zonder angst iets kapot te maken
3. Live zien wat de agent doet
4. Resultaten visueel terugzien
5. Ingrijpen of stoppen waar nodig

---

## 3. Conceptueel Model (Glossary)

### Agent
Het intelligente uitvoerende component dat intenties interpreteert en lokale acties uitvoert.

### Intent
Een door de gebruiker geformuleerde wens of instructie.

### Task
Een concrete, afgebakende uitvoering van een intent, met status en resultaat.

### View
Een visuele representatie van informatie of voortgang in de UI.

### UI Component
Een herbruikbaar, schema-gedreven bouwblok voor views.

### Event
Een onveranderbare melding van iets dat is gebeurd (append-only).

### Store
Een opslagplaats voor gestructureerde staat of kennis.

### Index
De opgebouwde representatie van systeemkennis en relaties.

---

## 4. Hoog-niveau Architectuur

### 4.1 Componenten
- Local Agent Runtime
- Backend (state, validatie, events)
- Frontend (UI renderer)
- Database (persistent state)

### 4.2 Basisprincipes
- Code is statisch, data is dynamisch
- Alles wat live verandert is schema-gedreven
- Geen directe mutaties, alleen events

---

## 5. Indexatie & Kennisopbouw

### 5.1 Gefaseerde indexatie
- Fase A: structuur en metadata
- Fase B: inhoud op basis van relevantie
- Fase C: verdieping op expliciete of impliciete intent

### 5.2 Transparantie
De gebruiker kan altijd zien:
- wat is geïndexeerd
- waarom iets relevant is
- wat (nog) niet is bekeken

---

## 6. Agent ↔ UI Contract

### 6.1 Basisregel
De agent **beschrijft staat**, de UI **rendert**.

### 6.2 Toegestane UI-acties
- open_view
- update_view
- close_view

### 6.3 UI-schema’s
- Declaratief
- Gevalideerd vóór renderen
- Geen HTML, CSS of JavaScript vanuit de agent

---

## 7. Frontend Architectuur

### 7.1 Rol van de frontend
- Pure renderer
- Geen businesslogica
- Geen beslissingen

### 7.2 State
- Ontvangen via backend events
- Read-only voor UI

---

## 8. Backend & State Management

### 8.1 Event-gedreven
- Append-only events
- Afgeleide state
- Volledig reproduceerbaar

### 8.2 Validatie
- Schema-validatie op elk niveau
- Ongeldige events worden geweigerd

---

## 9. Veiligheidsmodel (Fase 1: lokaal)

### 9.1 Standaardbeperkingen
- Read-only waar mogelijk
- Geen netwerk-egress zonder expliciete reden
- Geen toegang tot secrets zonder toestemming

### 9.2 Transparantie
Elke actie is:
- zichtbaar
- herleidbaar
- terug te draaien

---

## 10. Fasering & Scope

### Fase 1
- Desktop + browser UI
- Lokale agent
- UI-schema’s

### Fase 2
- Relay / remote access

### Fase 3
- Mobile client

### Doen we nu niet
- Enterprise policies
- Multi-user
- Autonome background agents

---

## 11. Niet-onderhandelbare Ontwerpregels

1. Local-first uitvoering
2. Schema-gedreven UI
3. Geen live code-aanpassingen
4. Transparantie boven snelheid
5. Controle blijft bij de gebruiker

---

## 12. Open Vragen (bewust leeg)

Dit hoofdstuk wordt alleen gevuld met expliciete, nog onbeantwoorde ontwerpvragen.

---

*Einde Canonical Design v0.1*

