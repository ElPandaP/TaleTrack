#!/bin/bash
# Run ONCE on the server (not locally) to bootstrap HTTPS for the first time.
# Requires the domain to already resolve to this server, port 80 to be open,
# and the app stack (postgres/backend/frontend) already up via docker-compose.yml.
set -e

compose() { docker compose -f docker-compose.yml -f docker-compose.proxy.yml "$@"; }

domains=(taletrack.app www.taletrack.app)
rsa_key_size=4096
data_path="./certbot"
staging=${LETSENCRYPT_STAGING:-0}

if [ -d "$data_path/conf/live/${domains[0]}" ]; then
  read -p "Existing certificate found for ${domains[0]}. Replace it? (y/N) " decision
  if [ "$decision" != "Y" ] && [ "$decision" != "y" ]; then
    exit
  fi
fi

if [ ! -e "$data_path/conf/options-ssl-nginx.conf" ] || [ ! -e "$data_path/conf/ssl-dhparams.pem" ]; then
  echo "### Downloading recommended TLS parameters ..."
  mkdir -p "$data_path/conf"
  curl -s https://raw.githubusercontent.com/certbot/certbot/master/certbot-nginx/certbot_nginx/_internal/tls_configs/options-ssl-nginx.conf > "$data_path/conf/options-ssl-nginx.conf"
  curl -s https://raw.githubusercontent.com/certbot/certbot/master/certbot/certbot/ssl-dhparams.pem > "$data_path/conf/ssl-dhparams.pem"
fi

echo "### Creating dummy certificate for ${domains[0]} ..."
path="/etc/letsencrypt/live/${domains[0]}"
mkdir -p "$data_path/conf/live/${domains[0]}"
compose run --rm --entrypoint "\
  openssl req -x509 -nodes -newkey rsa:$rsa_key_size -days 1 \
    -keyout '$path/privkey.pem' \
    -out '$path/fullchain.pem' \
    -subj '/CN=localhost'" certbot

echo "### Starting nginx ..."
compose up --force-recreate -d nginx

echo "### Deleting dummy certificate ..."
compose run --rm --entrypoint "\
  rm -Rf /etc/letsencrypt/live/${domains[0]} && \
  rm -Rf /etc/letsencrypt/archive/${domains[0]} && \
  rm -Rf /etc/letsencrypt/renewal/${domains[0]}.conf" certbot

echo "### Requesting real certificate from Let's Encrypt ..."
domain_args=""
for domain in "${domains[@]}"; do
  domain_args="$domain_args -d $domain"
done

staging_arg=""
if [ "$staging" != "0" ]; then
  staging_arg="--staging"
fi

compose run --rm --entrypoint "\
  certbot certonly --webroot -w /var/www/certbot \
    $staging_arg \
    --register-unsafely-without-email \
    $domain_args \
    --rsa-key-size $rsa_key_size \
    --agree-tos \
    --force-renewal" certbot

echo "### Reloading nginx ..."
compose exec nginx nginx -s reload

echo "### Done. https://${domains[0]} should be working now."
