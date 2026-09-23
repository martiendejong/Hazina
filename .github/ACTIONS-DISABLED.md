# GitHub Actions staat uit voor deze repository

Actions is hier uitgeschakeld (Settings > Actions > "Disable actions"), en dat is
opzettelijk.

**Reden: we willen geen kosten maken via GitHub.** Alle CI/CD voor Jengo-projecten
draait op onze eigen development server (build agents + deploy scripts over SSH),
niet op GitHub-hosted runners. Een tweede pipeline in de cloud levert alleen
dubbele en tegenstrijdige uitrol op, kost geld, en faalt zichtbaar rood zonder dat
iemand er iets mee doet.

**Voeg hier dus geen workflows toe.** Moet er iets automatisch draaien (build, test,
deploy, release), richt het in op de dev-server naast de bestaande build agents.

Uitgezet op 2026-09-10, in opdracht van Martien.
