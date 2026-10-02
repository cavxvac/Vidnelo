"""Offline catalog and static-label checks; run with Python 3."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1] / 'src'
rows = [line.split('|') for line in (root / 'Translations.txt').read_text(encoding='utf-8-sig').splitlines() if line]
assert rows[0] == ['key', 'pl', 'en', 'es', 'fr', 'de', 'pt-BR', 'it', 'uk', 'zh-Hans', 'ja', 'ko', 'hi', 'ar', 'tr', 'id']
assert len({row[0] for row in rows}) == len(rows), 'Duplicate key'
for row in rows[1:]:
    assert len(row) == 16 and all(value.strip() for value in row), row[0]
    placeholders = sorted(re.findall(r'\{\d+\}', row[1]))
    assert all(sorted(re.findall(r'\{\d+\}', value)) == placeholders for value in row[2:]), row[0]
sources = {row[1] for row in rows[1:]}
# Named controls below are filled with localized format templates at runtime.
dynamic = {'VideoFormatHint', 'AudioFormatHint', 'DownloadLabel'}
proper_names = {'Vidnelo', 'cavxvac'}
for name in ['MainWindow.xaml', 'SupportedSites.xaml', 'About.xaml']:
    for element in ET.parse(root / name).iter():
        for prop, value in element.attrib.items():
            if prop not in {'Text', 'Content', 'Header', 'Title', 'ToolTip', 'AutomationProperties.Name'}:
                continue
            if value.startswith('{') or not any(char.isalpha() for char in value):
                continue
            if element.attrib.get('{http://schemas.microsoft.com/winfx/2006/xaml}Name') in dynamic:
                continue
            assert value in sources or value in proper_names, (name, prop, value)
print('PASS: %s keys x 15 languages; unique keys, no empty cells, matching placeholders, static UI coverage.' % (len(rows) - 1))
