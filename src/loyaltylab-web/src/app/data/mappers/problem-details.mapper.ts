import { HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';

import type { AppError } from '../../domain';

@Injectable()
export class ProblemDetailsMapper {
  map(error: unknown): AppError {
    if (error instanceof HttpErrorResponse) {
      const body = asRecord(error.error);
      return {
        errorCode: readString(body, 'errorCode') ?? 'UNEXPECTED',
        message: readString(body, 'title') ?? error.message,
        status: error.status,
        correlationId: readString(body, 'correlationId'),
      };
    }

    return {
      errorCode: 'UNEXPECTED',
      message: error instanceof Error ? error.message : 'Something went wrong.',
      status: 0,
      correlationId: null,
    };
  }
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {};
}

function readString(record: Record<string, unknown>, key: string): string | null {
  const value = record[key];
  return typeof value === 'string' ? value : null;
}
