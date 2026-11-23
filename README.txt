DaneHud v1 (2026-11-23) — plugin Pulsara (Legacy) do Space Engineers

Okna z danymi grida, w którym siedzisz (kokpit / fotel / zdalne sterowanie), rysowane na HUD.
Gdy wstaniesz z fotela — okna znikają. Dane tylko z tego grida i podgridów na rotorach/tłokach
(statki zadokowane konektorem nie są liczone).

Polecenia czatu:
  /dane          — ustawienia (okna 1–6)
  /dane ukryj    — schowaj wszystkie okna
  /dane pokaz    — pokaż

Domyślne okna (jak na screenie z HudLcd + Automatic LCDs 2 + Isy):
  1 Rudy, 2 Ładownie (Ores/Ingots/Components + wodór), 3 Moc (+ prąd/wodór silników, ciąg),
  4 Sztabki (+ linia rafinerii), 5 i 6 Autocrafting Isy (połowa listy w każdym).

Elementy do wyboru w każdym oknie: nagłówek, pusty wiersz, lista przedmiotów (rudy/sztabki/komponenty/
amunicja/narzędzia/butle/wszystko, z wyborem co pokazać), zapełnienie ładowni (grupa albo tekst w nazwie),
zbiorniki H2/O2, moc (podsumowanie, czas zasilania, szczegóły źródeł), prąd: produkcja / silniki jonowe
i atmosferyczne, wodór: generatory / silniki / bilans / na ile starczy, ciąg w 6 stron z przyspieszeniem
i udźwigiem, rafinerie w jednej linii (liczba, czas do końca, rudy), autocrafting Isy (część listy),
tekst z dowolnego LCD.

Wygląd okna: położenie (suwaki z podglądem), szerokość, wielkość czcionki, odstęp wierszy, tło,
czcionka (zwykła / z cieniem / stała szerokość), kolory tytułu, tekstu, liczb, pasków, ostrzeżeń, tła.

Ładownie na żywo (wirtualny terminal): serwer przysyła klientowi zawartość kontenerów tylko przy otwartym
terminalu. Plugin zgłasza serwerowi "inventory otwarte na moim kokpicie" (to samo co K na kokpicie),
dlatego listy i paski ładowni się odświeżają. Można wyłączyć w /dane.

Ustawienia: %AppData%\SpaceEngineers\Plugins\DaneHud.xml, błędy: DaneHud.log w tym samym folderze.
