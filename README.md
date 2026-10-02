# Vidnelo 1.0

Uruchom **Vidnelo.exe**. Obsługa programu jest opisana w `INSTRUKCJA.txt`.

## Zawartość

- `Vidnelo.exe` — gotowa aplikacja.
- `tools/` — wymagane yt-dlp, FFmpeg, FFprobe i Deno oraz informacje o ich źródłach i licencji.
- `assets/` — ikony, oryginalne logo i opisy grafik. Skrót pulpitu korzysta z tego folderu.
- `src/` — aktualny kod aplikacji, interfejs WPF i 15 tłumaczeń.
- `tests/` — testy oraz niewielkie lokalne pliki multimedialne do ich uruchamiania.
- `scripts/` — pobieranie narzędzi, przygotowanie ikony i skrótu pulpitu.
- `docs/` — zachowany raport weryfikacji interfejsu.

## Kompilacja

W PowerShell uruchom `./build.ps1`. Wymagany jest kompilator .NET Framework w Windows.
Program wynikowy pozostaje obok `tools/`. Jeśli Vidnelo jest otwarte, zamknij je
przed zastąpieniem pliku EXE albo użyj `./build.ps1 -OutputName Vidnelo.dev.exe`.
Pliki pośrednie kompilatora powstają w `.build/` i można je usunąć po kompilacji.

## Weryfikacja

- `python tests/check-translations.py` sprawdza kompletność katalogu języków.
- `Vidnelo.exe --render-preview test-artifacts/ui` sprawdza interfejs i zapisuje podglądy.
- `Vidnelo.exe --localization-preview test-artifacts/languages` sprawdza wersje językowe.
- `python tests/test-server.py` udostępnia lokalne pliki testowe pod `127.0.0.1:18769`.
  Z uruchomionym serwerem: `Vidnelo.exe --ui-test test-artifacts/flow`.

Testy C# w `tests/` kompiluje się razem z `src/DownloadEngine.cs` (lub
`src/ProgressModel.cs` dla ProgressModelChecks) kompilatorem `csc.exe` z .NET Framework.
Wynikowe EXE należy umieścić w głównym folderze aplikacji, aby znalazły `tools/`.
Testy używają lokalnych plików i katalogu `test-artifacts/`.

`.build/`, `.downloads/`, `test-artifacts/` i robocze pliki EXE można usuwać po testach.
Ustawienia użytkownika są przechowywane oddzielnie w `%LOCALAPPDATA%/LinkDownloader`.

## Aktualizacje przez GitHub

Repozytorium wydań: https://github.com/cavxvac/Vidnelo/releases

Vidnelo sprawdza najnowsze stabilne wydanie w tle przy uruchomieniu. Nową wersję
sygnalizuje wyróżnieniem przycisku aktualizacji, bez przerywania pobierania.
Okno „Aktualizacje” pokazuje opis wydania i pozwala otworzyć jego stronę na
GitHubie, odłożyć aktualizację lub zapamiętać pominięcie danej wersji.
Ręczne sprawdzanie pokazuje także pominięte wersje. Brak połączenia podczas
startu nie wyświetla błędu; sprawdzanie ręczne informuje o problemie.

Ten wariant nie instaluje ani nie podmienia EXE automatycznie. Po pobraniu nowej
wersji należy zamknąć Vidnelo i zastąpić plik aplikacji. Ustawienia pozostają
w `%LOCALAPPDATA%/LinkDownloader`. Osobny przycisk „Aktualizuj yt-dlp” uruchamia
aktualizację silnika pobierania.

Wydania publikujemy z tagami `v1.0`, `v1.1`, `v1.2.1` itd. Numer w
`src/AssemblyInfo.cs` musi odpowiadać tagowi. Należy dołączyć skompilowaną
aplikację lub pakiet dystrybucyjny do GitHub Release — automatyczne archiwum
„Source code” nie jest gotową aplikacją. Wersje robocze i prerelease są pomijane.
Puste repozytorium bez Releases wyświetla „Brak opublikowanych wydań”.

## Autor i narzędzia

[cavxvac](https://github.com/cavxvac) · [gerardbinder.com](https://gerardbinder.com)

Vidnelo jest interfejsem graficznym dla [yt-dlp](https://github.com/yt-dlp/yt-dlp).
Narzędzia działają lokalnie. Informacje o dołączonych plikach znajdują się w `tools/`.
