#!/usr/bin/env bash

usage() {
  >&2 cat << EOF
Usage: ${BASH_SOURCE[0]// /\\ } [-h | --help] [-s <host> | --host <host>] [-p <port> | --port <port>] [--user <username>] [--pass <password>]
EOF
}

options=$(getopt -o "hs:p:" -l "help,host:,port:,user:,pass:" -- "$@")
[[ $? -eq 0 ]] || {
  usage
  exit 1
}
eval set -- "$options"

host='localhost'
port='8111'
user='Default'
while true; do
  case "$1" in
    -s | --host)
      host="$2"
      shift 2
      ;;
    -p | --port)
      port="$2"
      shift 2
      ;;
    --user)
      user="$2"
      shift 2
      ;;
    --pass)
      pass="$2"
      shift 2
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    --)
      shift
      break 
      ;;
  esac
done

if ! command -v jq >/dev/null 2>&1; then
  echo "Please install jq to use this script"
fi

if [[ $(curl -s --connect-timeout 2 -H 'Accept: application/json' "http://$host:$port/api/v3/Init/Status" | jq '.State==2') != 'true' ]]; then
  echo "Unabled to connect or server not running/started at target http://$host:$port"
  exit 1
fi

loginjson=$(jq --null-input --arg user "$user" --arg pass "$pass" '. + {user:$user, pass:$pass, device:"rename_bash_script"}')
apikey=$(curl -s -H "Content-Type: application/json" -d "$loginjson" "http://$host:$port/api/Auth" | jq -r '.apikey')

if ! [[ ${apikey//-/} =~ ^[[:xdigit:]]{32}$ ]]; then
  echo "Login did not return an api key, check --user and --pass"
  exit 1
fi

curl -s -H "apikey: $apikey" "http://$host:$port/api/v3/Renamer/Config" | jq
