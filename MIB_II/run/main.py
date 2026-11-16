import argparse
import json
import os

import requests


def parse_input():
    parser = argparse.ArgumentParser()

    # Development only
    parser.add_argument("--dry-run", action="store_true")

    return parser.parse_args()


def get_data(input_data):
    """Collect and return the JSON-serializable data for this connector."""
    # Replace this example with the logic that retrieves your connector data.
    return {"sysUptime": 123}


def push_data_to_edge_node(data):
    """Push connector data to the local Edge Node API."""
    run_secret = os.getenv("EDGE_RUN_SECRET")
    if not run_secret:
        raise ValueError(
            "Required environment variable 'EDGE_RUN_SECRET' is missing"
        )

    response = requests.post(
        "http://localhost:5016/api/data",
        json=data,
        headers={"runSecret": run_secret},
        timeout=30,
    )
    response.raise_for_status()
    return response


def main():
    input_data = parse_input()
    data = get_data(input_data)
   
    if input_data.dry_run:
        print(json.dumps(data, indent=2))
        return

    push_data_to_edge_node(data)


if __name__ == "__main__":
    main()
