param exists bool
param name string

resource existingAppJob 'Microsoft.App/jobs@2024-10-02-preview' existing = if (exists) {
  name: name
}

output containers array = exists ? existingAppJob.properties.template.containers : []
