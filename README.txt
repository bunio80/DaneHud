=====================================================================================================
DaneHud v2 (2026-11-23) — plugin Pulsara (Legacy) do Space Engineers            [English below]
=====================================================================================================

Okna z danymi grida, w którym siedzisz (kokpit / fotel / zdalne sterowanie), rysowane na HUD.
Zastępuje Automatic LCDs 2 + mod HudLcd, czyta też autocrafting Isy z jego LCD.
Gdy wstaniesz z fotela, okna znikają. Dane tylko z tego grida i podgridów na rotorach/tłokach.

Polecenia czatu (/dane albo /data):
  /dane          — ustawienia
  /dane ukryj    — schowaj wszystkie okna
  /dane pokaz    — pokaż
  /dane en       — język angielski, /dane pl — polski (też w menu)

Nowe w v2:
  * Osobne okna dla każdego grida. Statek bojowy, koparka i baza mają własne zestawy okien.
    Siedzisz w gridzie i otwierasz /dane: edytujesz okna TEGO grida (za pierwszym razem kopia szablonu).
    Szablon = okna gridów bez własnych ustawień. Przyciski: „Zapisz jako szablon”, „Usuń ustawienia grida”,
    „Wczytaj okna z:” (domyślne fabryczne, szablon albo inny grid). Grid jest rozpoznawany po Id,
    a gdy Id się zmieni (wklejony na nowo) — po nazwie.
  * Dowolna liczba okien: Nowe okno / Duplikuj / Usuń okno.
  * Polecenia z Automatic LCDs 2 jako elementy okna:
      Details (informacje bloku z terminala: NoN, X, od linii, do linii, ile linii)
        np. DetailsNoNX {[A] Shield Controller} {CapHp:} 2
      CustomData, Working (stan bloków), Count, Damage, Charge (napędy skokowe, baterie),
      Oxygen (ciśnienie, farmy), Amount (amunicja/uran/lód w blokach), PowerUsed, Time/Date,
      Distance (do GPS), Prop (opcja bloku), Docked, lot (prędkość, przyspieszenie, wysokość,
      grawitacja, masa, droga hamowania, tłumiki, pozycja), kolejka montażowni.
      Inventory z filtrem bloków (InvList), minimum i „tylko braki” (Missing), paski.
      Tekst do lewej / środka / prawej (Echo / Center / Right).
  * Filtr bloków jak w LCDs 2: tekst w nazwie, G:grupa, Typ:rodzaj (Typ:Refinery, Typ:Jump), * = wszystkie.
  * Menu poprawione: nic nie nachodzi, długie nazwy w oknach są skracane zamiast wchodzić na liczby.
  * Paliwo reaktorów w kg/t zamiast „1.5 k kg”.

Ustawienia: %AppData%\SpaceEngineers\Plugins\DaneHud.xml (kopia .bak przy każdym zapisie).
Ustawienia z v1 zostają i stają się szablonem. Błędy: DaneHud.log w tym samym folderze.

Ładownie na żywo (wirtualny terminal): serwer przysyła klientowi zawartość kontenerów tylko przy otwartym
terminalu. Plugin zgłasza serwerowi to samo, co K na kokpicie, więc listy i paski się odświeżają.

=====================================================================================================
DaneHud v2 — Pulsar (Legacy) plugin for Space Engineers
=====================================================================================================

Windows with data of the grid you are sitting in (cockpit / seat / remote control), drawn on the HUD.
Replaces Automatic LCDs 2 + the HudLcd mod and also reads Isy's autocrafting LCD.
The windows disappear when you leave the seat. Data comes only from that grid and its rotor/piston subgrids.

Chat commands (/data or /dane):
  /data          — settings
  /data hide     — hide all windows
  /data show     — show them again
  /data pl       — Polish, /data en — English (also in the menu)

New in v2:
  * Separate windows for each grid: a warship, a miner and a base each keep their own window set.
    Sitting in a grid and opening /data edits THAT grid's windows (the first time it copies the template).
    Template = windows of grids without their own settings. Buttons: "Save as template",
    "Delete grid settings", "Load windows from:" (factory defaults, template or another grid).
    A grid is recognised by its Id, or by its name when the Id changes (re-pasted grid).
  * Any number of windows: New window / Duplicate / Delete window.
  * Automatic LCDs 2 commands as window elements:
      Details (terminal block info: NoN, X, start line, stop line, line count)
        e.g. DetailsNoNX {[A] Shield Controller} {CapHp:} 2
      CustomData, Working (block status), Count, Damage, Charge (jump drives, batteries),
      Oxygen (pressure, farms), Amount (ammo/uranium/ice in blocks), PowerUsed, Time/Date,
      Distance (to a GPS), Prop (block property), Docked, flight (speed, acceleration, altitude,
      gravity, mass, stopping distance, dampeners, position), assembler queue.
      Inventory with a block filter (InvList), minimum and "only missing" (Missing), bars.
      Text left / centred / right (Echo / Center / Right).
  * Block filter like LCDs 2: text in the name, G:group, Type:kind (Type:Refinery, Type:Jump), * = all.
  * Fixed menu layout; long names in windows are shortened instead of overlapping the numbers.

Settings: %AppData%\SpaceEngineers\Plugins\DaneHud.xml (a .bak copy on every save).
v1 settings are kept and become the template. Errors: DaneHud.log in the same folder.

Live cargo (virtual terminal): the server sends container contents to a client only while the terminal is
open. The plugin tells the server the same thing as pressing K in the cockpit, so lists and bars stay live.
