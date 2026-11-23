=====================================================================================================
DaneHud v4 (2026-11-23) — plugin Pulsara (Legacy) do Space Engineers            [English below]
=====================================================================================================

Okna z danymi grida, w którym siedzisz, rysowane na HUD (zamiast Automatic LCDs 2 + HudLcd), oraz
MAGAZYN — części skryptu Isy wykonywane przez plugin, bez programowalnego bloku.

Polecenia czatu: /dane (albo /data) — ustawienia; /dane ukryj | pokaz; /dane pl | en.

Nowe w v4:
  * Kontenery specjalne jak u Isy: "Special" w nazwie, zawartość w CustomData (przedmiot=100 trzyma 100
    i oddaje nadmiar, 100M minimum, 100L tylko oddaje nadmiar, All bierze wszystko), priorytet [p1] [pmax] [pmin],
    wyższy priorytet może brać z niższego. Nowy kontener dostaje szablon CustomData jak u Isy. Sortowanie ich
    nie opróżnia. Rzeczy spoza listy są wynoszone do magazynu.
  * Pomijanie wybranych kontenerów: słowa w polu "Pomijane bloki" (Locked, Hidden, [No Sorting], !manual...).

Nowe w v3:
  * Styl tekstu okna: zwykły, pogrubiony, bardzo gruby, obrys, pogrubiony + obrys, cień
    (gra ma tylko 3 czcionki bez wersji pogrubionych — pogrubienie i obrys rysuje plugin).
    Kolor obrysu/cienia w kolorach okna.
  * MAGAZYN (/dane → przycisk „Magazyn (zamiast Isy)” w ustawieniach grida):
      - sortowanie do kontenerów ze słowem w nazwie: Ores, Ingots, Components, Tools, Ammo, Bottles
        (te same słowa co u Isy — nazwy kontenerów zostają),
      - rafinerie: każda ruda po równo we wszystkich (jak Twój Isy v4), priorytet rud (Stone, Platinum),
      - lód do generatorów H2/O2 (% wypełnienia), uran do reaktorów (duże / małe),
      - autocrafting wg limitów: montażownie dostają brakujące sztuki; limity wpisujesz w oknie
        „Limity autocraftingu” albo importujesz z LCD „Autocrafting” Isy jednym przyciskiem,
      - element HUD „Magazyn: stan i ostatnie ruchy”, autocrafting na HUD może brać limity z Magazynu.
    Magazyn działa, gdy siedzisz w gridzie (jak /asort w modzie Inventory Sorter). Przenosi przedmioty
    tak jak gracz w terminalu — serwer sprawdza uprawnienia i połączenie konwejerem. Gdy działa PB Isy,
    magazyn czeka (żeby się nie kłócić). Rafinerie na czas pracy nie pobierają rudy z konwejera;
    gdy wstaniesz z fotela, pobierają znowu (żeby pracowały też, gdy nie grasz).
  * Krótsze podpisy w oknach, poprawione zużycie H2 silników wodorowych (prąd).

=====================================================================================================
DaneHud v4 — Pulsar (Legacy) plugin for Space Engineers
=====================================================================================================

Windows with data of the grid you sit in, drawn on the HUD (instead of Automatic LCDs 2 + HudLcd), plus
STORAGE — parts of Isy's script run by the plugin, without a programmable block.

Chat: /data (or /dane) — settings; /data hide | show; /data pl | en.

New in v4:
  * Special containers like Isy: "Special" in the name, contents in CustomData (item=100 keeps 100 and removes excess,
    100M minimum, 100L only removes excess, All takes everything), priority [p1] [pmax] [pmin]; higher priority may take
    from lower. A new special container gets Isy's CustomData template. Sorting never drains them.
  * Skipping chosen containers: keywords in "Skipped blocks" (Locked, Hidden, [No Sorting], !manual...).

New in v3:
  * Window text style: normal, bold, extra bold, outline, bold + outline, drop shadow
    (the game has only 3 fonts without bold variants — bold and outline are drawn by the plugin).
  * STORAGE (/data → "Storage (instead of Isy)" button in a grid's settings):
      - sorting into containers with a keyword: Ores, Ingots, Components, Tools, Ammo, Bottles (Isy's words),
      - refineries: every ore split equally, ore priority (Stone, Platinum),
      - ice into H2/O2 generators (fill %), uranium into reactors (large / small),
      - autocrafting by limits; limits typed in "Autocrafting limits" or imported from Isy's "Autocrafting" LCD,
      - HUD element "Storage: status and recent moves"; HUD autocrafting can use Storage limits.
    Storage runs while you sit in the grid. It moves items like a player in the terminal (the server checks
    access and conveyor connection). It waits while Isy's PB runs. Refineries don't pull ore themselves while
    it runs; when you stand up they do again (so they keep working while you are offline).
