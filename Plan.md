# Nye filer til nye storage providers

Vi legger til nytt felt på attachment for storage provider.
Hvis tjeneste-eieren har en storage provider:
   * Når vi laster opp en ny attachment så tagger vi attachmenten med riktig storage provider ved initialize. 

Vi tagger manuelt attachmenten med eksisterende storage provider i SQL, men hvis storage account er null bruker den gamle løsningen
Dvs, lager ny download og upload funksjonalitet der storage provider gis som parameter og ikke er null.

# Etterpå
Fjerner nullability fra feltet i fremtidig PR etter den som kjører jobb for å migrere alle filer
