#!/bin/bash

# Wait for Elasticsearch to be ready
echo "Waiting for Elasticsearch to be ready..."
until curl -s http://localhost:9200/_cluster/health | grep -q '"status":"green"\|"status":"yellow"'; do
    sleep 5
done

# Create service account for Kibana
echo "Creating service account for Kibana..."
curl -X POST -u elastic:changeme \
  -H "Content-Type: application/json" \
  http://localhost:9200/_security/service/elastic/kibana/credential/token/kibana-token

# Get the service account token
echo "Getting service account token..."
TOKEN=$(curl -s -X POST -u elastic:changeme \
  -H "Content-Type: application/json" \
  http://localhost:9200/_security/service/elastic/kibana/credential/token/kibana-token | jq -r '.token.value')

# Export the token for docker-compose
echo "KIBANA_SERVICE_ACCOUNT_TOKEN=$TOKEN" > .env

echo "Setup complete! You can now start Kibana with: docker-compose up -d kibana" 