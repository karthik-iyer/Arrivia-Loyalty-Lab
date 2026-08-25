import { inject, Injectable } from '@angular/core';

import { err, ok, type Result } from '../../domain';
import { ProblemDetailsMapper } from '../mappers/problem-details.mapper';

@Injectable()
export class HttpResult {
  private readonly errors = inject(ProblemDetailsMapper);

  async capture<T>(run: () => Promise<T>): Promise<Result<T>> {
    try {
      return ok(await run());
    } catch (error: unknown) {
      return err(this.errors.map(error));
    }
  }
}
