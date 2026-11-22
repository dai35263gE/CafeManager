import { describe, expect, it } from 'vitest'
import { strFromU8, unzipSync } from 'fflate'
import { makeWorkbook, parseCsv, responseFileName } from './reportExports'

describe('report export helpers', () => {
  it('parses quoted commas, escaped quotes, line breaks, and a UTF-8 BOM', () => {
    expect(parseCsv('\uFEFFName,Note\r\n"Café, sữa","có ""đá""\nvà kem"\r\n')).toEqual([
      ['Name', 'Note'],
      ['Café, sữa', 'có "đá"\nvà kem'],
    ])
  })

  it('rejects malformed CSV rather than silently exporting a truncated workbook', () => {
    expect(() => parseCsv('Name,Note\n"unfinished')).toThrow('unterminated quoted field')
  })

  it('creates a valid Excel workbook with safe text cells and numeric amounts', () => {
    const files = unzipSync(makeWorkbook([
      ['Name', 'Revenue'],
      ['Café, "đặc biệt"', '25000.5'],
    ], 'revenue'))
    const worksheet = strFromU8(files['xl/worksheets/sheet1.xml'])
    expect(worksheet).toContain('Café, &quot;đặc biệt&quot;')
    expect(worksheet).toContain('<c r="B2"><v>25000.5</v></c>')
    expect(strFromU8(files['xl/workbook.xml'])).toContain('name="revenue"')
  })

  it('uses the UTF-8 filename when the response provides it', () => {
    expect(responseFileName("attachment; filename*=UTF-8''caf%C3%A9.csv", 'fallback.csv')).toBe('café.csv')
  })
})
