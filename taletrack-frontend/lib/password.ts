/**
 * Password rule shared by the registration and password reset forms. It mirrors the backend's
 * `PasswordPolicy`: 8 to 100 characters, with at least one uppercase letter and one digit.
 *
 * @module
 */

/** Maximum number of characters a password may have. */
export const PASSWORD_MAX_LENGTH = 100;

/** Which parts of the password rule a password meets. */
export interface PasswordChecks {
  /** At least 8 characters. */
  length: boolean;
  /** At least one uppercase letter. */
  upper: boolean;
  /** At least one digit. */
  number: boolean;
}

/** Checks a password against each part of the rule. */
export function checkPassword(password: string): PasswordChecks {
  return {
    length: password.length >= 8 && password.length <= PASSWORD_MAX_LENGTH,
    upper: /\p{Lu}/u.test(password),
    number: /\d/.test(password),
  };
}

/** Whether a password meets the whole rule. */
export function isPasswordValid(password: string): boolean {
  const c = checkPassword(password);
  return c.length && c.upper && c.number;
}
