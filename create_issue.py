#!/usr/bin/env python3
import os
import sys
import argparse
import requests

def load_dotenv():
    env_path = os.path.join(os.path.dirname(__file__), '.env')
    if os.path.exists(env_path):
        with open(env_path, 'r') as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith('#'):
                    key, val = line.split('=', 1)
                    os.environ[key.strip()] = val.strip()

def main():
    load_dotenv()
    
    parser = argparse.ArgumentParser(description="Create a GitHub issue automatically.")
    parser.add_argument('--title', required=True, help="Title of the issue")
    parser.add_argument('--body', required=True, help="Body/description of the issue")
    parser.add_argument('--repo', default="bozer00/jellyfin-plugin-letterboxd-sync", help="GitHub repo in owner/repo format")
    
    args = parser.parse_args()
    
    token = os.environ.get("GITHUB_TOKEN")
    if not token:
        print("Error: GITHUB_TOKEN environment variable or .env file entry not found.", file=sys.stderr)
        sys.exit(1)
        
    url = f"https://api.github.com/repos/{args.repo}/issues"
    headers = {
        "Authorization": f"token {token}",
        "Accept": "application/vnd.github.v3+json"
    }
    
    payload = {
        "title": args.title,
        "body": args.body
    }
    
    print(f"Creating issue in {args.repo}...")
    response = requests.post(url, json=payload, headers=headers)
    
    if response.status_code == 201:
        data = response.json()
        print(f"Success! Created issue #{data['number']}: {data['title']}")
        print(f"URL: {data['html_url']}")
        # Print issue number only to stdout for scripting
        # e.g., git commit -m "fix #$number"
        print(f"ISSUE_NUMBER={data['number']}")
    else:
        print(f"Error {response.status_code}: {response.text}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
