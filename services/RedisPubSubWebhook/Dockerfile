FROM alpine:3.12.4

RUN apk add --no-cache \ 
    openssl libunwind libc6-compat=1.1.24-r10 \
    curl libstdc++ libintl zlib nghttp2-libs

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT true

WORKDIR /app

COPY /app/out ./

RUN chmod +x ./RedisPubSubWebhook

EXPOSE 8080

ENTRYPOINT ["./RedisPubSubWebhook"]