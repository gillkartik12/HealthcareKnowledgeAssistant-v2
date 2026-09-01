variable "aws_region" {
  description = "AWS region used by the application"
  type        = string
  default     = "us-east-1"
}

variable "localstack_endpoint" {
  description = "LocalStack AWS endpoint"
  type        = string
  default     = "http://localhost:4566"
}

variable "s3_bucket_name" {
  description = "Bucket containing uploaded healthcare documents"
  type        = string
  default     = "healthcare-documents"
}

variable "ingestion_queue_name" {
  description = "Queue containing document ingestion jobs"
  type        = string
  default     = "document-ingestion-queue"
}

variable "dlq_name" {
  description = "Dead-letter queue for failed ingestion jobs"
  type        = string
  default     = "document-ingestion-dlq"
}

variable "dynamodb_table_name" {
  description = "Document processing status table"
  type        = string
  default     = "document-processing"
}