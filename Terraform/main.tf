resource "aws_s3_bucket" "documents" {
  bucket = var.s3_bucket_name
}

resource "aws_sqs_queue" "document_dlq" {
  name = var.dlq_name
}

resource "aws_sqs_queue" "document_ingestion" {
  name = var.ingestion_queue_name

  visibility_timeout_seconds = 60

  receive_wait_time_seconds = 10

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.document_dlq.arn
    maxReceiveCount     = 3
  })
}

resource "aws_dynamodb_table" "document_processing" {
  name         = var.dynamodb_table_name
  billing_mode = "PAY_PER_REQUEST"

  hash_key = "DocumentId"

  attribute {
    name = "DocumentId"
    type = "S"
  }
}