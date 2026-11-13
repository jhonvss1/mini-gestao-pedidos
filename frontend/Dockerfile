# Contexto de build: a pasta frontend/ (docker compose build frontend).
FROM node:24-alpine AS build
WORKDIR /app

# Dependências em camada própria: só refaz quando package.json ou o lock mudam.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.29-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
EXPOSE 80
