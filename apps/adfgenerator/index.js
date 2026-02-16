const defaultSchema = require('@atlaskit/adf-schema')
const JSONTransformer = require('@atlaskit/editor-json-transformer')
const MarkdownTransformer = require('@atlaskit/editor-markdown-transformer')
const jsonTransformer = new JSONTransformer.JSONTransformer();
const markdownTransformer = new MarkdownTransformer.MarkdownTransformer(defaultSchema.defaultSchema);
const express = require('express')
const app = express()
const port = process.env['PORT'] || 3000;
app.use(express.json()) // for parsing application/json
app.use(express.urlencoded({ extended: true })) // for parsing application/x-www-form-urlencoded

function sendError(res, statusCode, message, details = null) {
  console.error(`[${statusCode}] ${message}`, details || '');
  const response = { error: message };
  if (details) {
    response.details = details;
  }
  return res.status(statusCode).json(response);
}

function validateADF(data) {
  if (!data || (typeof data === 'object' && !Array.isArray(data) && Object.keys(data).length === 0)) {
    return { valid: false, error: 'Payload cannot be empty' };
  }
  
  // Check if it's a single object or array
  const items = Array.isArray(data) ? data : [data];
  
  for (const item of items) {
    if (typeof item !== 'object' || item === null) {
      return { valid: false, error: 'ADF must be an object or array of objects' };
    }
    if (!item.type || !item.version) {
      return { valid: false, error: 'ADF must have "type" and "version" properties' };
    }
  }
  
  return { valid: true };
}

function convertADFToMarkdown(adfDocument) {
  try {
    // Custom ADF to Markdown converter
    // Note: @atlaskit/editor-markdown-transformer.encode() is not implemented
    if (adfDocument.type === 'doc') {
      const markdown = adfDocument.content.map(convertNode).join('\n\n');
      return { success: true, markdown };
    }
    return { success: true, markdown: '' };
  } catch (error) {
    console.error('ADF to Markdown conversion error:', error);
    return { success: false, error: error.message };
  }
}

function convertNode(node) {
  // Handle different node types
  switch (node.type) {
    case 'paragraph':
      return node.content ? node.content.map(convertInline).join('') : '';
    
    case 'heading':
      const level = node.attrs?.level || 1;
      const headingText = node.content ? node.content.map(convertInline).join('') : '';
      return '#'.repeat(level) + ' ' + headingText;
    
    case 'bulletList':
      return node.content ? node.content.map(item => convertListItem(item, '-')).join('\n') : '';
    
    case 'orderedList':
      return node.content ? node.content.map((item, index) => convertListItem(item, `${index + 1}.`)).join('\n') : '';
    
    case 'codeBlock':
      const language = node.attrs?.language || '';
      const code = node.content ? node.content.map(c => c.text || '').join('') : '';
      return '```' + language + '\n' + code + '\n```';
    
    case 'blockquote':
      const quoteContent = node.content ? node.content.map(convertNode).join('\n') : '';
      return quoteContent.split('\n').map(line => '> ' + line).join('\n');
    
    case 'rule':
      return '---';
    
    case 'hardBreak':
      return '  \n';
    
    default:
      // For unknown node types, try to extract text content
      if (node.content) {
        return node.content.map(convertNode).join('');
      }
      return '';
  }
}

function convertListItem(item, prefix) {
  if (item.type !== 'listItem') return '';
  const content = item.content ? item.content.map(convertNode).join('\n') : '';
  // Indent nested content
  const lines = content.split('\n');
  return prefix + ' ' + lines[0] + (lines.length > 1 ? '\n' + lines.slice(1).map(l => '  ' + l).join('\n') : '');
}

function convertInline(inline) {
  if (inline.type === 'text') {
    let text = inline.text || '';
    
    // Apply marks (formatting)
    if (inline.marks) {
      for (const mark of inline.marks) {
        switch (mark.type) {
          case 'strong':
            text = `**${text}**`;
            break;
          case 'em':
            text = `*${text}*`;
            break;
          case 'code':
            text = `\`${text}\``;
            break;
          case 'strike':
            text = `~~${text}~~`;
            break;
          case 'underline':
            // Markdown doesn't have native underline, use HTML or ignore
            text = `<u>${text}</u>`;
            break;
          case 'link':
            const href = mark.attrs?.href || '#';
            text = `[${text}](${href})`;
            break;
        }
      }
    }
    
    return text;
  } else if (inline.type === 'hardBreak') {
    return '  \n';
  } else if (inline.type === 'mention') {
    // Mentions don't have direct markdown equivalent
    const mentionText = inline.attrs?.text || '@unknown';
    return mentionText;
  } else if (inline.type === 'emoji') {
    return inline.attrs?.shortName || '';
  }
  
  // For other inline types, try to recurse
  if (inline.content) {
    return inline.content.map(convertInline).join('');
  }
  
  return '';
}

app.post('/adf-to-markdown', (req, res) => {
  try {
    const validation = validateADF(req.body);
    if (!validation.valid) {
      return sendError(res, 400, 'Invalid ADF structure', validation.error);
    }
    
    // Handle single object
    if (!Array.isArray(req.body)) {
      const result = convertADFToMarkdown(req.body);
      if (!result.success) {
        return sendError(res, 500, 'ADF conversion failed', result.error);
      }
      res.set('Content-Type', 'text/plain');
      return res.status(200).send(result.markdown);
    }
    
    // Handle batch
    const results = req.body.map((adf, index) => {
      const result = convertADFToMarkdown(adf);
      if (!result.success) {
        return { index, error: result.error, markdown: null };
      }
      return { index, markdown: result.markdown, error: null };
    });
    
    // Check if any conversions failed
    const failures = results.filter(r => r.error !== null);
    if (failures.length > 0) {
      return res.status(207).json({ 
        status: 'partial_success',
        results: results 
      });
    }
    
    return res.status(200).json(results.map(r => r.markdown));
  } catch (error) {
    return sendError(res, 500, 'Internal server error', error.message);
  }
});

app.get('/', (req, res) => {
  // console.log(req.body.text)
  const markdownDocument = '**test** world';
  const adfDocument = jsonTransformer.encode(markdownTransformer.parse(markdownDocument));
  res.json(adfDocument)
})

app.post('/', (req, res) => {
  console.log(req.body)
  const markdownDocument = req.body.text;
  const adfDocument = jsonTransformer.encode(markdownTransformer.parse(markdownDocument));
  res.send(adfDocument)
})

app.get('/health', (req, res) => {
  res.status(200).json({
    status: 'healthy',
    service: 'adfgenerator',
    timestamp: new Date().toISOString()
  });
});

app.use((err, req, res, next) => {
  console.error('Uncaught error:', err);
  res.status(500).json({ 
    error: 'Internal server error',
    message: err.message 
  });
});

app.listen(port, () => {
  console.log(`Example app listening on port ${port}`)
})