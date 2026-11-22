import { strToU8, zipSync } from 'fflate'

export type ExportReport = 'revenue' | 'top-items' | 'payment-methods' | 'inventory'

export function parseCsv(text: string): string[][] {
  const rows: string[][] = []
  let row: string[] = []
  let field = ''
  let quoted = false
  const source = text.replace(/^\uFEFF/, '')
  for (let index = 0; index < source.length; index++) {
    const character = source[index]
    if (quoted) {
      if (character === '"' && source[index + 1] === '"') { field += '"'; index++ }
      else if (character === '"') quoted = false
      else field += character
    } else if (character === '"' && field.length === 0) quoted = true
    else if (character === ',') { row.push(field); field = '' }
    else if (character === '\n' || character === '\r') {
      if (character === '\r' && source[index + 1] === '\n') index++
      row.push(field)
      if (row.some(value => value.length > 0)) rows.push(row)
      row = []
      field = ''
    } else field += character
  }
  if (field.length || row.length) {
    row.push(field)
    if (row.some(value => value.length > 0)) rows.push(row)
  }
  if (quoted) throw new Error('CSV contains an unterminated quoted field.')
  return rows
}

function columnName(index: number) {
  let value = index + 1
  let result = ''
  while (value > 0) {
    value--
    result = String.fromCharCode(65 + value % 26) + result
    value = Math.floor(value / 26)
  }
  return result
}

function xml(value: string) {
  let validXml = ''
  for (const character of value) {
    const code = character.charCodeAt(0)
    if (code <= 8 || code === 11 || code === 12 || (code >= 14 && code <= 31)) continue
    validXml += character
  }
  return validXml.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
}

export function makeWorkbook(rows: string[][], report: ExportReport) {
  if (!rows.length) throw new Error('A workbook requires at least a header row.')
  const headers = rows[0]
  const textColumns = new Set(['Day', 'MenuItemId', 'Name', 'PaymentMethod', 'Code', 'Unit', 'ExpiryDate'])
  const sheetRows = rows.map((row, rowIndex) => `<row r="${rowIndex + 1}">${row.map((value, columnIndex) => {
    const address = `${columnName(columnIndex)}${rowIndex + 1}`
    const isNumber = rowIndex > 0 && !textColumns.has(headers[columnIndex]) && /^-?(?:\d+\.?\d*|\.\d+)$/.test(value)
    return isNumber
      ? `<c r="${address}"><v>${value}</v></c>`
      : `<c r="${address}" t="inlineStr"><is><t xml:space="preserve">${xml(value)}</t></is></c>`
  }).join('')}</row>`).join('')
  const forbiddenSheetNameCharacters = new Set(['[', ']', '*', '?', ':', '/', '\\'])
  const sheetName = [...report].map(character =>
    forbiddenSheetNameCharacters.has(character) ? '_' : character).join('').slice(0, 31) || 'Report'
  return zipSync({
    '[Content_Types].xml': strToU8('<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>'),
    '_rels/.rels': strToU8('<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>'),
    'xl/workbook.xml': strToU8(`<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="${xml(sheetName)}" sheetId="1" r:id="rId1"/></sheets></workbook>`),
    'xl/_rels/workbook.xml.rels': strToU8('<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>'),
    'xl/worksheets/sheet1.xml': strToU8(`<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>${sheetRows}</sheetData></worksheet>`),
  }, { level: 6 })
}

export function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  try { anchor.href = url; anchor.download = fileName; document.body.append(anchor); anchor.click() }
  finally { anchor.remove(); window.setTimeout(() => URL.revokeObjectURL(url), 1000) }
}

export function responseFileName(disposition: string, fallback: string) {
  const encoded = /filename\*\s*=\s*UTF-8''([^;]+)/i.exec(disposition)?.[1]
  const plain = /filename\s*=\s*(?:"([^"]+)"|([^;]+))/i.exec(disposition)
  let fileName = plain?.[1] || plain?.[2]?.trim() || fallback
  if (encoded) {
    try { fileName = decodeURIComponent(encoded.trim()) }
    catch (error) { if (!(error instanceof URIError)) throw error }
  }
  return fileName.replace(/[\\/:*?"<>|]/g, '_')
}
