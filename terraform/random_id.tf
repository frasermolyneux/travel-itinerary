resource "random_id" "environment_id" {
  byte_length = 6
}

resource "random_id" "storage" {
  byte_length = 3
}

resource "random_id" "maps_key" {
  byte_length = 2

  keepers = {
    environment = var.environment
  }
}
