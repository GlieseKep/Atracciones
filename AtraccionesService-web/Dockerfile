# Build estático con Vite y servido por nginx. Las variables VITE_* se fijan en tiempo de build.
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
ARG VITE_API_BASE_URL
ARG VITE_CATALOG_SOURCE=auto
ARG VITE_OAUTH_AUTHORIZATION_URL
ARG VITE_OAUTH_TOKEN_URL
ARG VITE_OAUTH_LOGOUT_URL
ARG VITE_OAUTH_CLIENT_ID
ARG VITE_OAUTH_AUDIENCE
ARG VITE_OAUTH_REDIRECT_URI
ARG VITE_OAUTH_SCOPES
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
