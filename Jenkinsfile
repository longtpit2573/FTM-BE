// Jenkinsfile for FTM Backend - Simplified version for webhook testing
// Full CI/CD will require Docker-enabled agent

pipeline {
    agent any
    
    environment {
        // ACR Configuration
        ACR_NAME = 'acrftmbackenddev'
        ACR_REGISTRY = "${ACR_NAME}.azurecr.io"
        IMAGE_NAME = 'ftm-backend'
        IMAGE_TAG = "${env.BUILD_NUMBER}"
        
        // GitOps Configuration
        GITOPS_REPO = 'https://github.com/longtpit2573/Infrastructure.git'
        GITOPS_PATH = 'applications/overlays/dev'
    }
    
    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 10, unit: 'MINUTES')
    }
    
    stages {
        stage('📋 Info') {
            steps {
                echo '========================================='
                echo '  FTM Backend Pipeline - Webhook Test'
                echo '========================================='
                echo "Build Number: ${env.BUILD_NUMBER}"
                echo "Branch: ${env.BRANCH_NAME}"
                echo "Workspace: ${env.WORKSPACE}"
                echo "Image Tag: ${IMAGE_TAG}"
                echo '========================================='
            }
        }
        
        stage('🔍 Checkout') {
            steps {
                echo 'Checking out source code...'
                checkout scm
                script {
                    env.GIT_COMMIT_SHORT = sh(
                        script: "git rev-parse --short HEAD",
                        returnStdout: true
                    ).trim()
                    env.GIT_COMMIT_MSG = sh(
                        script: 'git log -1 --pretty=%B',
                        returnStdout: true
                    ).trim()
                }
                echo "✅ Git Commit: ${env.GIT_COMMIT_SHORT}"
                echo "✅ Message: ${env.GIT_COMMIT_MSG}"
            }
        }
        
        stage('✅ Webhook Test Success') {
            steps {
                echo '========================================='
                echo '  🎉 WEBHOOK IS WORKING!'
                echo '========================================='
                echo "Pipeline triggered successfully by GitHub webhook"
                echo "Next step: Setup Docker-enabled agent for full CI/CD"
                echo '========================================='
            }
        }
    }
    
    post {
        success {
            echo '========================================='
            echo '  ✅ BUILD SUCCESS'
            echo '========================================='
            echo "Image Tag: ${IMAGE_TAG}"
            echo "Commit: ${env.GIT_COMMIT_SHORT}"
            echo '========================================='
        }
        failure {
            echo '========================================='
            echo '  ❌ BUILD FAILED'
            echo '========================================='
            echo "Check: ${env.BUILD_URL}"
            echo '========================================='
        }
    }
}
