# syntax=docker/dockerfile:1.6
FROM node:20-bullseye
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci || npm install
COPY . .
EXPOSE 7070
CMD ["npm","run","dev"]
