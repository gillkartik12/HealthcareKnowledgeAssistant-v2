output "s3_bucket_name" {
  value = aws_s3_bucket.documents.bucket
}

output "ingestion_queue_url" {
  value = aws_sqs_queue.document_ingestion.url
}

output "dead_letter_queue_url" {
  value = aws_sqs_queue.document_dlq.url
}

output "dynamodb_table_name" {
  value = aws_dynamodb_table.document_processing.name
}