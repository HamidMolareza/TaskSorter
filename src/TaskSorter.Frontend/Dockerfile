FROM docker.arvancloud.ir/node:24-alpine AS build
WORKDIR /app

COPY src/TaskSorter.Frontend/package*.json ./
RUN npm ci

COPY src/TaskSorter.Frontend/ ./
RUN npm run build

FROM docker.arvancloud.ir/nginx:1.29-alpine AS final
COPY src/TaskSorter.Frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
