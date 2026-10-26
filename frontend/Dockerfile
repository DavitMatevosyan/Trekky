# Trekky web image: the built React app served by Caddy, which also terminates HTTPS and proxies the API on the
# same origin (ADR-0004, ADR-0010). Build context: frontend/.
FROM node:22-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM caddy:2-alpine
COPY Caddyfile /etc/caddy/Caddyfile
COPY --from=build /src/dist /srv
