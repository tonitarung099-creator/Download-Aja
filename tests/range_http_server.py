#!/usr/bin/env python3
import argparse
import re
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--file", required=True)
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--log", required=True)
    parser.add_argument("--delay-ms", type=float, default=0.0)
    return parser.parse_args()


ARGS = parse_args()
SOURCE = Path(ARGS.file).resolve()
LOG = Path(ARGS.log).resolve()
DELAY = max(0.0, ARGS.delay_ms / 1000.0)


class RangeHandler(BaseHTTPRequestHandler):
    server_version = "DownloadAjaRangeTest/1.0"

    def do_HEAD(self):
        self._serve(send_body=False)

    def do_GET(self):
        self._serve(send_body=True)

    def _serve(self, send_body):
        if self.path.split("?", 1)[0] != "/sample.mp4":
            self.send_error(404)
            return

        size = SOURCE.stat().st_size
        start = 0
        end = size - 1
        status = 200
        raw_range = self.headers.get("Range")

        if raw_range:
            match = re.fullmatch(r"bytes=(\d+)-(\d*)", raw_range.strip())
            if not match:
                self.send_error(416)
                return

            start = int(match.group(1))
            if start >= size:
                self.send_response(416)
                self.send_header("Content-Range", f"bytes */{size}")
                self.end_headers()
                return

            if match.group(2):
                end = min(int(match.group(2)), size - 1)

            if end < start:
                self.send_error(416)
                return

            status = 206

        LOG.parent.mkdir(parents=True, exist_ok=True)
        with LOG.open("a", encoding="utf-8") as log:
            log.write(f"RANGE {raw_range or 'NONE'} START {start} END {end}\n")
            log.flush()

        length = end - start + 1
        self.send_response(status)
        self.send_header("Content-Type", "video/mp4")
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("Content-Length", str(length))
        if status == 206:
            self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
        self.end_headers()

        if not send_body:
            return

        try:
            with SOURCE.open("rb") as stream:
                stream.seek(start)
                remaining = length
                while remaining > 0:
                    chunk = stream.read(min(64 * 1024, remaining))
                    if not chunk:
                        break
                    self.wfile.write(chunk)
                    self.wfile.flush()
                    remaining -= len(chunk)
                    if DELAY:
                        time.sleep(DELAY)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def log_message(self, fmt, *args):
        return


if __name__ == "__main__":
    server = ThreadingHTTPServer(("127.0.0.1", ARGS.port), RangeHandler)
    server.daemon_threads = True
    server.serve_forever()
