# syntax=docker/dockerfile:1.6
FROM node:20-bullseye
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci || npm install
COPY . .
EXPOSE 3000
CMD ["npm","run","dev","--","--host","0.0.0.0"]
