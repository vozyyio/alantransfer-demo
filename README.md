# AlanTransfer — Demo (Ortak İş Defteri)

Tur ve transfer operasyonu için **ortak iş defteri** mantığını gösteren küçük, çalışan bir demo.
İlanda en çok vurgulanan üç riski çözer:

1. **Ortak iş kaydı** — tek rezervasyon, tüm kanallarda (web, acente, portal) aynı kayıt üzerinden ilerler.
2. **Havuzda eş zamanlı kabul kontrolü** — iki sürücü aynı işi aynı anda kabul etmeye çalışırsa yalnızca biri alır (SQL Server `rowversion` / optimistic concurrency, ikinciye `409 Conflict`).
3. **Rol bazlı yetki** — yetki API katmanında uygulanır; bir acente başka acentenin kaydını çekemez (`403`).

Teknoloji: **ASP.NET Core 8 Web API + SQL Server**, Swagger, JWT.

## Çalıştırma (Docker)

```bash
git clone <repo-url> alantransfer-demo
cd alantransfer-demo
docker compose up -d --build
```

- Demo arayüzü: `http://SUNUCU:8088`
- Swagger: `http://SUNUCU:8088/swagger`
- İlk açılışta SQL Server hazır olana kadar API birkaç saniye bekler.

Cloudflare arkasında bir alt domain (örn. `demo.yolcio.com`) `8088` portuna yönlendirilir; SSL Cloudflare tarafında biter.

## Demo kullanıcıları (şifre: `1234`)

| Kullanıcı | Rol |
| --- | --- |
| admin | Admin |
| operasyon | Operasyon |
| muhasebe | Muhasebe |
| acenteA / acenteB | Acente |
| sofor1 / sofor2 | Sürücü |

## Eş zamanlılık testini görmek

1. `operasyon` ile giriş yap, bir işi **Havuza bırak**.
2. 4. karttaki kutuya o işin ID'sini yaz, **"sofor1 + sofor2 aynı anda kabul etsin"** butonuna bas.
3. Log'da biri `200 OK`, diğeri `409` görür — iş yalnızca bir sürücüye bağlanır.

> Not: Şifreler ve JWT anahtarı yalnızca demo içindir; üretimde hash'lenir ve dışarı alınır.
