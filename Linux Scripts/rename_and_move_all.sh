#!/usr/bin/env bash
if ! type jq > /dev/null; then
  echo "Please install jq to use this script"
fi
if [ $# -eq 0 ]; then
	echo "Pass name of script (on server) as first parameter"
	echo "Provides preview of filenames after running the script"
	exit -1
fi
if [[ $(basename "$1") == *.lua ]]; then
  echo "Use the script name on the server, not the file name, re-add the script if it is updated"
  exit -1
fi
if [ -z "$(curl -s "http://localhost:8111/v1/RenameScript" | jq -c ".[] | select(.ScriptName==\"$1\")")" ]; then
  echo "Script $1 does not exist on the server, try adding it first"
  exit -1
fi
curl -s "http://localhost:8111/v1/File/Rename/RandomPreview/2147483647/1" | \
jq '.[].VideoLocalID' | \
while read -r vlocal_id
do
	curl -s "http://localhost:8111/v1/File/Rename/$vlocal_id/"$1"/true" | jq '.NewFileName'
done