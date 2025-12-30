import errno
import json
import os
import subprocess
import sys

# Actions
Write = 0
Append = 1
Run = 2

steps = json.load(sys.stdin)

def mkdir_p(path):
    try:
        os.makedirs(path)
    except OSError as exc: # Python >2.5
        if exc.errno == errno.EEXIST and os.path.isdir(path):
            pass
        else: raise

for step in steps:
	if step["action"] == Write:
		print("Write" + step["path"])
		mkdir_p(os.path.dirname(step["path"]))
		file = open(step["path"], "w")
		file.write(step["text"])
		file.close()
	elif step["action"] == Append:
		print("Append" + step["path"])
		mkdir_p(os.path.dirname(step["path"]))
		file = open(step["path"], "a")
		file.write(step["text"])
		file.close()
	elif step["action"] == Run:
		print("Run" + str(step["command"]))
		subprocess.run(step["command"], shell=True)
