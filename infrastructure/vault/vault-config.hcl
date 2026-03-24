storage "file" {
  path = "/vault/data"
}

listener "tcp" {
  address     = "0.0.0.0:8200"
  # TODO: Production MUST set tls_disable = 0 and configure TLS certificates
  tls_disable = 1
}

ui            = true
# TODO: Production MUST set disable_mlock = false and configure OS mlock support
disable_mlock = true
api_addr      = "http://0.0.0.0:8200"
