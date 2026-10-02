from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
import os
import time
import shutil

root = Path(__file__).resolve().parents[1] / 'test-artifacts'
root.mkdir(exist_ok=True)
for fixture in (Path(__file__).resolve().parent / 'fixtures').iterdir():
    if fixture.is_file():
        shutil.copy2(fixture, root / fixture.name)
os.chdir(root)

class Handler(SimpleHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith('/slow.mp4'):
            self.send_response(200)
            self.send_header('Content-Type', 'video/mp4')
            self.send_header('Content-Length', '100000000')
            self.end_headers()
            try:
                data = (root / 'sample.mp4').read_bytes()
                for _ in range(10000):
                    self.wfile.write(data)
                    self.wfile.flush()
                    time.sleep(.2)
            except (BrokenPipeError, ConnectionResetError):
                pass
        else:
            super().do_GET()

ThreadingHTTPServer(('127.0.0.1', 18769), Handler).serve_forever()
