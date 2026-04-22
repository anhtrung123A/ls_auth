# API Docs

Base URL: `/`

## 1) Create User
- **Method/Path**: `POST /users`
- **Input (JSON body)**:
```json
{
  "loginId": "john.doe",
  "registeredAtUtc": "2026-04-22T12:00:00Z",
  "registeredByEmail": "admin@example.com",
  "updatedByEmail": "admin@example.com"
}
```
- **Output**:
  - `201 Created`: tạo user thành công
  - `409 Conflict`: trùng `loginId`
  - `400 Bad Request`: sai định dạng input
- **Response mẫu (201)**:
```json
{
  "success": true,
  "status": 201,
  "data": {
    "id": 1,
    "loginId": "john.doe",
    "registeredAtUtc": "2026-04-22T12:00:00Z",
    "registeredByEmail": "admin@example.com",
    "updatedByEmail": "admin@example.com",
    "generatedPassword": "Abc123!Xy"
  }
}
```
- **Response mẫu (409)**:
```json
{
  "success": false,
  "status": 409,
  "data": null,
  "error": {
    "code": "USR_409_DUPLICATE_LOGIN_ID",
    "message": "user with provided login_id already exists."
  }
}
```

## 2) Login
- **Method/Path**: `POST /auth/login`
- **Input (JSON body)**:
```json
{
  "loginId": "john.doe",
  "password": "Abc123!Xy"
}
```
- **Output**:
  - `200 OK`: đăng nhập thành công
  - `401 Unauthorized`: sai thông tin đăng nhập
  - `400 Bad Request`: thiếu/sai input
- **Response mẫu (200)**:
```json
{
  "success": true,
  "status": 200,
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "accessTokenExpiresAtUtc": "2026-04-22T12:15:00Z",
    "refreshToken": "rfr_8d2ca8e6a4...",
    "refreshTokenExpiresAtUtc": "2026-04-29T12:00:00Z"
  }
}
```
- **Response mẫu (401)**:
```json
{
  "success": false,
  "status": 401,
  "data": null,
  "error": {
    "code": "AUTH_401_INVALID_CREDENTIALS",
    "message": "invalid login credentials."
  }
}
```

## 3) Refresh Token
- **Method/Path**: `POST /auth/refresh`
- **Input (JSON body)**:
```json
{
  "refreshToken": "rfr_8d2ca8e6a4..."
}
```
- **Output**:
  - `200 OK`: cấp token mới thành công
  - `401 Unauthorized`: refresh token không hợp lệ/hết hạn/bị revoke
  - `400 Bad Request`: thiếu input
- **Response mẫu (200)**:
```json
{
  "success": true,
  "status": 200,
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "accessTokenExpiresAtUtc": "2026-04-22T12:30:00Z",
    "refreshToken": "rfr_new_3107cb...",
    "refreshTokenExpiresAtUtc": "2026-04-29T12:15:00Z"
  }
}
```
- **Response mẫu (401 - token hết hạn/revoke)**:
```json
{
  "success": false,
  "status": 401,
  "data": null,
  "error": {
    "code": "AUTH_401_REFRESH_TOKEN_REVOKED_OR_EXPIRED",
    "message": "refresh token is revoked or expired."
  }
}
```

## 4) Logout
- **Method/Path**: `POST /auth/logout`
- **Input (JSON body)**:
```json
{
  "refreshToken": "rfr_new_3107cb..."
}
```
- **Output**:
  - `200 OK`: logout thành công (thu hồi refresh token)
  - `400 Bad Request`: thiếu input
- **Response mẫu (200)**:
```json
{
  "success": true,
  "status": 200,
  "data": {
    "revoked": true
  }
}
```

## Response format chung
- **Success**:
```json
{
  "success": true,
  "status": 200,
  "data": {}
}
```
- **Error**:
```json
{
  "success": false,
  "status": 400,
  "data": null,
  "error": {
    "code": "REQ_400_VALIDATION",
    "message": "..."
  }
}
```
