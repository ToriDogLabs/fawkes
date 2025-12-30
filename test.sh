#!/bin/sh

script_name=$(basename "$0")
short=a:k:e:s:p:i:t:
long=access_key:,secret_key:,endpoint:,server:,path:,id:,target_time:

TEMP=$(getopt -o $short --long $long --name "$script_name" -- "$@")

eval set -- "${TEMP}"

while :; do
	case "${1}" in
		-k | --secret_key       ) secret_key=$2;             shift 2 ;;
		-a | --access_key     ) access_key=$2;           shift 2 ;;
		-e | --endpoint ) endpoint=$2;       shift 2 ;;
		-s | --server ) server=$2;       shift 2 ;;
		-p | --path) path=$2;       shift 2 ;;
		-i | --id ) id=$2;       shift 2 ;;
		-t | --target_time) target_time=$2;       shift 2 ;;
		--                ) shift;                 break ;;
		*                 ) echo "Error parsing"; exit 1 ;;
	esac
done
echo "access=${access_key} secret=${secret_key} end=${endpoint} path=${path} server=${server} id=${id} tt=${target_time}"

