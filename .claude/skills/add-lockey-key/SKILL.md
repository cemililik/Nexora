---
name: add-lockey-key
description: Add a new localization key (lockey_) across backend usages and frontend translation JSON files (en + tr minimum) per LOCALIZATION_STANDARDS.md. Use when the user asks to "add a translation", "new message key", "add lockey", or introduces any user-facing string.
---

# Add Lockey Key

Nexora has **zero hardcoded user-facing strings**. Every message is a `lockey_` key resolved on the frontend.

## Key naming
Format: `lockey_{scope}_{context}_{descriptor}`
- `scope`: module name (`identity`, `contacts`, `crm`) OR `validation`, `error`, `common`.
- Snake_case, lowercase, descriptive.

Examples:
- `lockey_contacts_create_success`
- `lockey_identity_login_invalid_credentials`
- `lockey_validation_required`
- `lockey_error_unexpected`

## Backend usage (all mandatory)
- `Result.Success(dto, LocalizedMessage.Of("lockey_..."))`
- `Result.Failure<T>(LocalizedMessage.Of("lockey_..."))`
- `throw new DomainException("lockey_...")` (only from domain entities)
- `RuleFor(x => x.Field).NotEmpty().WithMessage("lockey_validation_required")`

Backend NEVER returns translated text — always returns the key.

## Frontend usage
- React admin: `const { t } = useTranslation('{module}'); t('lockey_...')`
- Next.js portal: `const t = useTranslations('{module}'); t('lockey_...')`
- Never write raw text in JSX.

## Files to update when adding a key
1. `src/Clients/nexora-admin/src/locales/en/{module}.json`
2. `src/Clients/nexora-admin/src/locales/tr/{module}.json`
3. If used by portal too: `src/Clients/nexora-portal/locales/{en,tr}/{module}.json`
4. Backend call site (handler/validator/domain).

Minimum languages: **en + tr**. Keep the same key in both files — never leave one missing.

## Verification
- `grep` the key across repo — should appear in backend call site + 2 locale files minimum.
- No hardcoded English/Turkish strings in handlers, validators, or JSX.
