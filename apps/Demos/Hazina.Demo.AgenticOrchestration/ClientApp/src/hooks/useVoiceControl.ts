import { useState, useRef, useCallback, useEffect } from 'react'

// TypeScript declarations for Web Speech API
interface SpeechRecognitionEvent extends Event {
  results: SpeechRecognitionResultList
  resultIndex: number
}

interface SpeechRecognitionResultList {
  length: number
  item(index: number): SpeechRecognitionResult
  [index: number]: SpeechRecognitionResult
}

interface SpeechRecognitionResult {
  length: number
  item(index: number): SpeechRecognitionAlternative
  [index: number]: SpeechRecognitionAlternative
  isFinal: boolean
}

interface SpeechRecognitionAlternative {
  transcript: string
  confidence: number
}

interface SpeechRecognitionErrorEvent extends Event {
  error: string
  message?: string
}

interface SpeechRecognition extends EventTarget {
  continuous: boolean
  interimResults: boolean
  lang: string
  start(): void
  stop(): void
  abort(): void
  onstart: ((this: SpeechRecognition, ev: Event) => void) | null
  onend: ((this: SpeechRecognition, ev: Event) => void) | null
  onresult: ((this: SpeechRecognition, ev: SpeechRecognitionEvent) => void) | null
  onerror: ((this: SpeechRecognition, ev: SpeechRecognitionErrorEvent) => void) | null
  onnomatch: ((this: SpeechRecognition, ev: Event) => void) | null
}

declare global {
  interface Window {
    SpeechRecognition: new () => SpeechRecognition
    webkitSpeechRecognition: new () => SpeechRecognition
  }
}

export interface VoiceControlState {
  isSupported: boolean
  isListening: boolean
  interimTranscript: string
  error: string | null
}

export interface VoiceControlActions {
  toggle: () => void
  start: () => void
  stop: () => void
}

export function useVoiceControl(
  onTranscript: (text: string) => void
): [VoiceControlState, VoiceControlActions] {
  const [isSupported, setIsSupported] = useState(false)
  const [isListening, setIsListening] = useState(false)
  const [interimTranscript, setInterimTranscript] = useState('')
  const [error, setError] = useState<string | null>(null)

  const recognitionRef = useRef<SpeechRecognition | null>(null)
  const finalTranscriptRef = useRef('')

  // Initialize speech recognition
  useEffect(() => {
    const SpeechRecognitionClass = window.SpeechRecognition || window.webkitSpeechRecognition

    if (!SpeechRecognitionClass) {
      setIsSupported(false)
      return
    }

    setIsSupported(true)
    const recognition = new SpeechRecognitionClass()

    // Configure recognition
    recognition.continuous = true
    recognition.interimResults = true
    recognition.lang = 'en-US'

    recognition.onstart = () => {
      setIsListening(true)
      setError(null)
      finalTranscriptRef.current = ''
      setInterimTranscript('')
    }

    recognition.onend = () => {
      setIsListening(false)
      setInterimTranscript('')

      // Send final transcript if we have one
      if (finalTranscriptRef.current.trim()) {
        onTranscript(finalTranscriptRef.current.trim())
        finalTranscriptRef.current = ''
      }
    }

    recognition.onresult = (event: SpeechRecognitionEvent) => {
      let interim = ''
      let final = ''

      for (let i = event.resultIndex; i < event.results.length; i++) {
        const result = event.results[i]
        if (result.isFinal) {
          final += result[0].transcript
        } else {
          interim += result[0].transcript
        }
      }

      if (final) {
        finalTranscriptRef.current += final
      }
      setInterimTranscript(interim)
    }

    recognition.onerror = (event: SpeechRecognitionErrorEvent) => {
      let errorMessage = 'Speech recognition error'

      switch (event.error) {
        case 'no-speech':
          errorMessage = 'No speech detected'
          break
        case 'audio-capture':
          errorMessage = 'No microphone found'
          break
        case 'not-allowed':
          errorMessage = 'Microphone access denied'
          break
        case 'network':
          errorMessage = 'Network error'
          break
        case 'aborted':
          // User aborted, not an error
          return
        default:
          errorMessage = `Error: ${event.error}`
      }

      setError(errorMessage)
      setIsListening(false)
    }

    recognition.onnomatch = () => {
      setError('Could not understand speech')
    }

    recognitionRef.current = recognition

    return () => {
      if (recognitionRef.current) {
        try {
          recognitionRef.current.abort()
        } catch {
          // Ignore cleanup errors
        }
      }
    }
  }, [onTranscript])

  const start = useCallback(() => {
    if (!recognitionRef.current || isListening) return

    setError(null)
    try {
      recognitionRef.current.start()
    } catch (err) {
      setError('Failed to start voice recognition')
      console.error('Voice recognition start error:', err)
    }
  }, [isListening])

  const stop = useCallback(() => {
    if (!recognitionRef.current || !isListening) return

    try {
      recognitionRef.current.stop()
    } catch (err) {
      console.error('Voice recognition stop error:', err)
    }
  }, [isListening])

  const toggle = useCallback(() => {
    if (isListening) {
      stop()
    } else {
      start()
    }
  }, [isListening, start, stop])

  const state: VoiceControlState = {
    isSupported,
    isListening,
    interimTranscript,
    error,
  }

  const actions: VoiceControlActions = {
    toggle,
    start,
    stop,
  }

  return [state, actions]
}
