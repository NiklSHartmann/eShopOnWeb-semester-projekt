# Artifact storage strategy

Pipelinen producerer to deployment-artefakter i build-jobbet. Begge bygges af samme `dotnet publish`-output, så koden kun kompileres én gang.

| Artefakt | Hvor | Navn | Opbevaring |
|---|---|---|---|
| Zip-fil | GitHub Actions artifacts (under den enkelte kørsel) | `eshop-web-<commit-sha>.zip` | 30 dage, slettes automatisk |
| Docker image | GitHub Container Registry (GHCR) | `ghcr.io/<bruger>/eshop-web:<commit-sha>` | Indtil det slettes manuelt |

## Valg af lager

GHCR er valgt, fordi det er indbygget i GitHub. Pipelinen kan pushe med det automatiske `GITHUB_TOKEN`, så der ikke skal oprettes ekstra konti eller secrets, og public images er gratis. Zip-filen gemmes som workflow-artefakt, fordi den primært bruges som dokumentation af build-outputtet og som alternativ deploy-form. Docker imaget er det, der faktisk deployes.

## Navngivning og sporbarhed

Begge artefakter navngives med commit-SHA'en. Man kan derfor altid se, præcis hvilken version af koden et artefakt stammer fra, og finde koden i Git ud fra artefaktet og omvendt.

Ved deploy bruges imagets digest (`@sha256:...`) i stedet for tagget. Et tag kan i princippet flyttes til et andet image; et digest kan ikke, fordi det er en hash af indholdet. Det garanterer, at staging og production kører præcis det image, der blev testet.

## Oprydning og rollback

Zip-filer ryddes automatisk op efter 30 dage. Docker images bliver liggende i GHCR, hvilket er fint i et semesterprojekts omfang og gør rollback nemt: gamle versioner kan deployes igen fra Render-dashboardet (Manual Deploy) uden at bygge noget. I et længerevarende projekt ville man indføre en retention policy, der fx sletter images ældre end 90 dage, men aldrig det, der kører i et miljø.
